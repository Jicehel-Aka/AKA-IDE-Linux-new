using System;
using System.Collections.Generic;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Vérifie la STRUCTURE d'un code source, sans l'exécuter ni construire un arbre syntaxique complet —
    /// voir CodeCheckResult pour la portée exacte (ce que ça attrape, et ce que ça n'attrape pas).
    ///
    /// Lua : un tokeniseur complet de la grammaire lexicale de Lua 5.1 (chaînes courtes/longues,
    /// commentaires courts/longs, avec le bon comptage des "=" des crochets longs "[=[...]=]"), plus un
    /// empilement des mots-clés qui ouvrent un bloc (if/do/function → attendent "end" ; repeat →
    /// attend "until") pour repérer un "end"/"until" oublié, en trop, ou substitué à l'autre. `while` et
    /// `for` ne poussent RIEN directement : c'est leur propre mot-clé `do` qui porte l'obligation de
    /// fermeture — un `do` manquant après `for`/`while` n'est donc pas signalé À CET ENDROIT précis
    /// (l'erreur ressort quand même, mais possiblement rapportée sur le bloc englobant plutôt que sur
    /// le `for`/`while` fautif). Ce n'est PAS un analyseur de grammaire complet : une expression
    /// mal formée mais structurellement équilibrée (parenthèses/mots-clés bien comptés) ne sera pas
    /// détectée — seul un vrai `luaL_loadstring` (exécuté sur l'appareil ou via un interpréteur Lua)
    /// donnerait une garantie complète.
    ///
    /// MicroPython : contrôle un cran plus léger que Lua sur la grammaire elle-même (pas de suivi des
    /// mots-clés de bloc if/def/while/for → nécessiterait de distinguer un ":" de bloc d'un ":" de
    /// tranche/dictionnaire/annotation, ambigu sans un vrai analyseur), mais l'INDENTATION — la faute la
    /// plus commune en Python — EST vérifiée, avec l'algorithme du vrai tokeniseur CPython : une pile de
    /// niveaux, et une désindentation qui ne retombe sur aucun niveau connu, ou un mélange de
    /// tabulations/espaces dont le résultat dépend de la largeur de tabulation supposée (le TabError de
    /// Python), sont tous deux signalés. NON RECONNU : une continuation de ligne par "\" en fin de ligne
    /// (rare dans du code MicroPython typique, qui utilise plutôt des parenthèses) ; l'équilibre des
    /// parenthèses/crochets/accolades et les chaînes (simples ou triple-guillemets) non refermées.
    /// </summary>
    public class CodeCheckService : ICodeCheckService
    {
        public CodeCheckResult Check(string code, CodeLanguage language) => language switch
        {
            CodeLanguage.Lua => CheckLua(code ?? string.Empty),
            CodeLanguage.MicroPython => CheckPython(code ?? string.Empty),
            _ => CodeCheckResult.Success,
        };

        // ================================================================================= Lua

        private static readonly HashSet<string> LuaEndOpeners = new() { "if", "do", "function" };

        private static CodeCheckResult CheckLua(string code)
        {
            int line = 1, col = 1, i = 0, n = code.Length;
            var brackets = new Stack<(char Ch, int Line, int Col)>();
            var blocks = new Stack<(string Expect, int Line, int Col, string Keyword)>();

            void Adv(char c)
            {
                if (c == '\n') { line++; col = 1; } else { col++; }
            }
            void AdvTo(int target)   // avance i jusqu'à target-1 inclus, en mettant ligne/colonne à jour
            {
                while (i < target) { Adv(code[i]); i++; }
            }

            while (i < n)
            {
                char c = code[i];

                if (c is ' ' or '\t' or '\r' or '\n') { Adv(c); i++; continue; }

                // -- commentaire court ou long
                if (c == '-' && i + 1 < n && code[i + 1] == '-')
                {
                    int j = i + 2, eq = 0;
                    if (j < n && code[j] == '[')
                    {
                        int k = j + 1;
                        while (k < n && code[k] == '=') { eq++; k++; }
                        if (k < n && code[k] == '[')
                        {
                            int startLine = line, startCol = col;
                            AdvTo(k + 1);
                            string closer = "]" + new string('=', eq) + "]";
                            int closeIdx = code.IndexOf(closer, i, StringComparison.Ordinal);
                            if (closeIdx < 0)
                                return new CodeCheckResult(false, startLine, startCol,
                                    $"Commentaire long non terminé (« {closer} » manquant).");
                            AdvTo(closeIdx + closer.Length);
                            continue;
                        }
                    }
                    while (i < n && code[i] != '\n') { Adv(code[i]); i++; }
                    continue;
                }

                // chaîne longue [=[ ... ]=]
                if (c == '[')
                {
                    int j = i + 1, eq = 0;
                    while (j < n && code[j] == '=') { eq++; j++; }
                    if (j < n && code[j] == '[')
                    {
                        int startLine = line, startCol = col;
                        AdvTo(j + 1);
                        string closer = "]" + new string('=', eq) + "]";
                        int closeIdx = code.IndexOf(closer, i, StringComparison.Ordinal);
                        if (closeIdx < 0)
                            return new CodeCheckResult(false, startLine, startCol,
                                $"Chaîne longue non terminée (« {closer} » manquant).");
                        AdvTo(closeIdx + closer.Length);
                        continue;
                    }
                    // sinon : simple crochet ouvrant, traité plus bas
                }

                // chaîne courte '...' ou "..."
                if (c is '"' or '\'')
                {
                    char quote = c;
                    int startLine = line, startCol = col;
                    Adv(c); i++;
                    bool closed = false;
                    while (i < n)
                    {
                        char cc = code[i];
                        if (cc == '\\' && i + 1 < n) { Adv(cc); i++; Adv(code[i]); i++; continue; }
                        if (cc == quote) { Adv(cc); i++; closed = true; break; }
                        if (cc == '\n') break;
                        Adv(cc); i++;
                    }
                    if (!closed)
                        return new CodeCheckResult(false, startLine, startCol,
                            "Chaîne de caractères non terminée avant la fin de la ligne.");
                    continue;
                }

                // identifiant / mot-clé
                if (char.IsLetter(c) || c == '_')
                {
                    int startLine = line, startCol = col, start = i;
                    while (i < n && (char.IsLetterOrDigit(code[i]) || code[i] == '_')) { Adv(code[i]); i++; }
                    string word = code.Substring(start, i - start);

                    if (LuaEndOpeners.Contains(word))
                        blocks.Push(("end", startLine, startCol, word));
                    else if (word == "repeat")
                        blocks.Push(("until", startLine, startCol, word));
                    else if (word == "end")
                    {
                        if (blocks.Count == 0)
                            return new CodeCheckResult(false, startLine, startCol,
                                "« end » en trop : aucun bloc ouvert ne l'attend ici.");
                        var top = blocks.Peek();
                        if (top.Expect != "end")
                            return new CodeCheckResult(false, startLine, startCol,
                                $"« end » inattendu : le bloc « {top.Keyword} » ouvert ligne {top.Line} attend « until », pas « end ».");
                        blocks.Pop();
                    }
                    else if (word == "until")
                    {
                        if (blocks.Count == 0)
                            return new CodeCheckResult(false, startLine, startCol,
                                "« until » en trop : aucun bloc « repeat » ouvert ne l'attend ici.");
                        var top = blocks.Peek();
                        if (top.Expect != "until")
                            return new CodeCheckResult(false, startLine, startCol,
                                $"« until » inattendu : le bloc « {top.Keyword} » ouvert ligne {top.Line} attend « end », pas « until ».");
                        blocks.Pop();
                    }
                    continue;
                }

                if (char.IsDigit(c))
                {
                    Adv(c); i++;
                    while (i < n && (char.IsLetterOrDigit(code[i]) || code[i] == '.')) { Adv(code[i]); i++; }
                    continue;
                }

                if (c is '(' or '[' or '{')
                {
                    brackets.Push((c, line, col));
                    Adv(c); i++;
                    continue;
                }
                if (c is ')' or ']' or '}')
                {
                    char want = c == ')' ? '(' : c == ']' ? '[' : '{';
                    if (brackets.Count == 0 || brackets.Peek().Ch != want)
                        return new CodeCheckResult(false, line, col, $"« {c} » inattendu ici.");
                    brackets.Pop();
                    Adv(c); i++;
                    continue;
                }

                Adv(c); i++;
            }

            if (brackets.Count > 0)
            {
                var (ch, l, cc) = brackets.Peek();
                return new CodeCheckResult(false, l, cc, $"« {ch} » jamais refermé.");
            }
            if (blocks.Count > 0)
            {
                var top = blocks.Peek();
                string need = top.Expect == "end" ? "« end »" : "« until »";
                return new CodeCheckResult(false, top.Line, top.Col,
                    $"Bloc « {top.Keyword} » jamais refermé : {need} manquant.");
            }
            return CodeCheckResult.Success;
        }

        // ================================================================================= Python / MicroPython

        private static CodeCheckResult CheckPython(string code)
        {
            int line = 1, col = 1, i = 0, n = code.Length;
            var brackets = new Stack<(char Ch, int Line, int Col)>();
            // Pile d'indentation façon tokeniseur CPython : chaque niveau garde SA largeur calculée de
            // deux façons (tabulation = 1 colonne, tabulation = saut au multiple de 8 suivant). Si le
            // sens de la comparaison (plus/moins/égal) diffère entre les deux calculs, l'indentation est
            // ambiguë -- exactement le TabError que Python lui-même refuserait d'exécuter.
            var indent = new Stack<(int W8, int W1)>();
            indent.Push((0, 0));
            bool atLineStart = true;

            void Adv(char c)
            {
                if (c == '\n') { line++; col = 1; } else { col++; }
            }
            void AdvTo(int target)
            {
                while (i < target) { Adv(code[i]); i++; }
            }

            while (i < n)
            {
                // -- Début d'une ligne physique : mesurer son indentation, seulement si on n'est pas à
                // l'intérieur de parenthèses ouvertes (une ligne de continuation n'a pas d'indentation
                // significative en Python). Les lignes vides ou uniquement commentaires n'affectent
                // jamais la pile. NON RECONNU : une continuation par "\" en fin de ligne (rare dans du
                // code MicroPython typique, qui utilise plutôt des parenthèses) -- une ligne ainsi
                // continuée est traitée comme une nouvelle ligne logique.
                if (atLineStart)
                {
                    atLineStart = false;
                    if (brackets.Count == 0)
                    {
                        int startLine = line, startCol = col;
                        int w8 = 0, w1 = 0;
                        while (i < n && (code[i] == ' ' || code[i] == '\t'))
                        {
                            if (code[i] == ' ') { w8 += 1; w1 += 1; }
                            else { w8 = (w8 / 8 + 1) * 8; w1 += 1; }
                            Adv(code[i]); i++;
                        }
                        bool blankOrComment = i >= n || code[i] == '\n' || code[i] == '\r' || code[i] == '#';
                        if (!blankOrComment)
                        {
                            var top = indent.Peek();
                            int sign8 = Math.Sign(w8 - top.W8);
                            int sign1 = Math.Sign(w1 - top.W1);
                            if (sign8 != sign1)
                                return new CodeCheckResult(false, startLine, startCol,
                                    "Indentation ambiguë : le mélange de tabulations et d'espaces sur cette " +
                                    "ligne donne un résultat différent selon la largeur de tabulation.");

                            if (sign8 > 0)
                            {
                                indent.Push((w8, w1));
                            }
                            else if (sign8 < 0)
                            {
                                while (indent.Count > 1 && indent.Peek().W8 > w8)
                                {
                                    indent.Pop();
                                    var nt = indent.Peek();
                                    if (Math.Sign(w8 - nt.W8) != Math.Sign(w1 - nt.W1))
                                        return new CodeCheckResult(false, startLine, startCol,
                                            "Indentation ambiguë : mélange de tabulations et d'espaces " +
                                            "incohérent avec un niveau englobant.");
                                }
                                if (indent.Peek().W8 != w8)
                                    return new CodeCheckResult(false, startLine, startCol,
                                        "Désindentation qui ne correspond à aucun niveau d'indentation précédent.");
                            }
                        }
                    }
                }

                char c = code[i];

                if (c == '\n') { Adv(c); i++; atLineStart = true; continue; }
                if (c is ' ' or '\t' or '\r') { Adv(c); i++; continue; }

                if (c == '#')
                {
                    while (i < n && code[i] != '\n') { Adv(code[i]); i++; }
                    continue;
                }

                // chaîne triple-guillemets : '''...''' ou """..."""
                if ((c == '"' || c == '\'') && i + 2 < n && code[i + 1] == c && code[i + 2] == c)
                {
                    int startLine = line, startCol = col;
                    string triple = new string(c, 3);
                    AdvTo(i + 3);
                    int closeIdx = code.IndexOf(triple, i, StringComparison.Ordinal);
                    if (closeIdx < 0)
                        return new CodeCheckResult(false, startLine, startCol,
                            "Chaîne triple-guillemets non terminée.");
                    AdvTo(closeIdx + 3);
                    continue;
                }

                // chaîne simple '...' ou "..."
                if (c is '"' or '\'')
                {
                    char quote = c;
                    int startLine = line, startCol = col;
                    Adv(c); i++;
                    bool closed = false;
                    while (i < n)
                    {
                        char cc = code[i];
                        if (cc == '\\' && i + 1 < n) { Adv(cc); i++; Adv(code[i]); i++; continue; }
                        if (cc == quote) { Adv(cc); i++; closed = true; break; }
                        if (cc == '\n') break;
                        Adv(cc); i++;
                    }
                    if (!closed)
                        return new CodeCheckResult(false, startLine, startCol,
                            "Chaîne de caractères non terminée avant la fin de la ligne.");
                    continue;
                }

                if (c is '(' or '[' or '{')
                {
                    brackets.Push((c, line, col));
                    Adv(c); i++;
                    continue;
                }
                if (c is ')' or ']' or '}')
                {
                    char want = c == ')' ? '(' : c == ']' ? '[' : '{';
                    if (brackets.Count == 0 || brackets.Peek().Ch != want)
                        return new CodeCheckResult(false, line, col, $"« {c} » inattendu ici.");
                    brackets.Pop();
                    Adv(c); i++;
                    continue;
                }

                Adv(c); i++;
            }

            if (brackets.Count > 0)
            {
                var (ch, l, cc) = brackets.Peek();
                return new CodeCheckResult(false, l, cc, $"« {ch} » jamais refermé.");
            }
            return CodeCheckResult.Success;
        }
    }
}
