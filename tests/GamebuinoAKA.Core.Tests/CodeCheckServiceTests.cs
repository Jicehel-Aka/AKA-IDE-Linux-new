using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    /// <summary>
    /// Cas rejoués depuis un banc de test manuel (52 cas, 52 succès) exécuté avec le vrai compilateur/
    /// runtime .NET 10 pendant l'écriture de CodeCheckService — voir sa doc pour la portée exacte du
    /// contrôle (équilibre structurel + indentation Python façon tokeniseur CPython, pas une grammaire
    /// complète).
    /// </summary>
    public class CodeCheckServiceTests
    {
        private readonly CodeCheckService _svc = new();

        [Theory]
        [InlineData("")]
        [InlineData("if x then print(1) end")]
        [InlineData("function f(a,b) return a+b end")]
        [InlineData("while x do y = y + 1 end")]
        [InlineData("for i=1,10 do print(i) end")]
        [InlineData("for k,v in pairs(t) do print(k,v) end")]
        [InlineData("repeat x = x + 1 until x > 10")]
        [InlineData("if a then b() elseif c then d() else e() end")]
        [InlineData("local f = function() return 1 end")]
        [InlineData("do do do end end end")]
        [InlineData("-- commentaire\nprint(1)")]
        [InlineData("--[[ un\ncommentaire\nlong ]] print(1)")]
        [InlineData("--[==[ texte ]] pas fini ]==] print(1)")]
        [InlineData("local s = [[texte sur\nplusieurs lignes]]")]
        [InlineData("local s = 'if end function' print(s)")]
        public void Lua_ValidCode_IsOk(string code) => Assert.True(_svc.Check(code, CodeLanguage.Lua).Ok);

        [Theory]
        [InlineData("function f() print(1)")]              // end manquant
        [InlineData("print(1) end")]                        // end en trop
        [InlineData("until x > 10")]                        // until sans repeat
        [InlineData("repeat x = x + 1 end")]                // repeat clos par end au lieu de until
        [InlineData("print(1")]                             // parenthèse non fermée
        [InlineData("print(1))")]                           // parenthèse en trop
        [InlineData("local t = (1, [2)]")]                  // parenthèses croisées
        [InlineData("local s = 'texte sans fin\nprint(1)")] // chaîne non fermée
        [InlineData("local s = [[texte sans fin")]          // chaîne longue non fermée
        [InlineData("--[[ jamais fini")]                    // commentaire long non fermé
        public void Lua_InvalidCode_IsNotOk(string code) => Assert.False(_svc.Check(code, CodeLanguage.Lua).Ok);

        [Fact]
        public void Lua_RealCassebriquesSnippet_IsOk()
        {
            const string code = @"
local function seChevauchent(ax, ay, aw, ah, bx, by, bw, bh)
  return ax < bx + bw and bx < ax + aw and ay < by + bh and by < ay + ah
end
function love.load()
  for i = 1, 3 do
    if i == 2 then
      print('deux')
    end
  end
end
";
            Assert.True(_svc.Check(code, CodeLanguage.Lua).Ok);
        }

        [Fact]
        public void Lua_MissingEnd_ReportsCorrectLine()
        {
            const string code = "local function f()\n  print(1)\n";   // "end" manquant, ouvert ligne 1
            var r = _svc.Check(code, CodeLanguage.Lua);
            Assert.False(r.Ok);
            Assert.Equal(1, r.Line);
        }

        [Theory]
        [InlineData("")]
        [InlineData("import aka\nwhile True:\n    aka.clear(0)\n    aka.update()\n")]
        [InlineData("# commentaire\nx = 1")]
        [InlineData("s = '''texte\nsur plusieurs lignes'''")]
        [InlineData("s = 'bonjour'")]
        [InlineData("x = (1 +\n     2 +\n     3)")]
        [InlineData("if True:\n    x = 1\n    y = 2\n")]
        [InlineData("if True:\n    if False:\n        x = 1\n    y = 2\nz = 3\n")]
        [InlineData("if True:\n\tx = 1\n\tif False:\n\t\ty = 2\n")]                          // tabulations pures : cohérent
        [InlineData("if True:\n    x = 1\n# commentaire a la marge\n    y = 2\n")]           // commentaire hors-jeu, sans effet
        [InlineData("if True:\n    x = 1\n\n    y = 2\n")]                                   // ligne vide au milieu d'un bloc
        [InlineData("x = (\n1 +\n      2\n)\nif True:\n    y = 1\n")]                        // libre entre parenthèses
        [InlineData("def f():\n    '''\n  pas aligne\n        autre decalage\n    '''\n    return 1\n")]  // indentation DANS une chaîne : ignorée
        public void Python_ValidCode_IsOk(string code) =>
            Assert.True(_svc.Check(code, CodeLanguage.MicroPython).Ok);

        [Theory]
        [InlineData("print(1")]
        [InlineData("x = [1, 2, 3")]
        [InlineData("s = 'texte sans fin\nprint(1)")]
        [InlineData("s = '''texte sans fin")]
        [InlineData("print(1))")]
        public void Python_InvalidCode_IsNotOk(string code) =>
            Assert.False(_svc.Check(code, CodeLanguage.MicroPython).Ok);

        [Fact]
        public void Python_MixedTabsAndSpaces_IsAmbiguous()
        {
            // ligne 2 indentée à l'espace, ligne 3 à la tabulation : ambigu selon la largeur de tabulation
            var r = _svc.Check("if True:\n    x = 1\n\tif False:\n\t\ty = 2\n", CodeLanguage.MicroPython);
            Assert.False(r.Ok);
            Assert.Equal(3, r.Line);
        }

        [Theory]
        [InlineData("if True:\n    if False:\n        x = 1\n  y = 2\n", 4)]   // désindentation vers un niveau inexistant
        [InlineData("if True:\n        x = 1\n    y = 2\n", 3)]               // idem, un seul niveau de bloc
        public void Python_DedentMismatch_ReportsLine(string code, int expectedLine)
        {
            var r = _svc.Check(code, CodeLanguage.MicroPython);
            Assert.False(r.Ok);
            Assert.Equal(expectedLine, r.Line);
        }

        [Fact]
        public void Python_RealMainPySnippet_IsOk()
        {
            const string code = @"
def list_games():
    all_files = aka.list_py('/sdcard/py')
    games = [f for f in all_files if f not in _EXCLUDE]
    return games
";
            Assert.True(_svc.Check(code, CodeLanguage.MicroPython).Ok);
        }

        [Fact]
        public void Python_RealMainPyLoopSnippet_IsOk()
        {
            const string code = "while True:\n    if not games:\n        aka.update()\n        aka.clear(0, 0, 0)\n" +
                                 "        continue\n\n    if sel >= len(games):\n        sel = 0\n\n    p = aka.pressed()\n" +
                                 "    if (p & aka.UP) and sel > 0:\n        sel -= 1\n";
            Assert.True(_svc.Check(code, CodeLanguage.MicroPython).Ok);
        }

        [Fact]
        public void UnknownLanguage_AlwaysOk()
        {
            // BASIC (ou tout futur langage sans vérificateur dédié) : jamais bloquant en attendant.
            Assert.True(_svc.Check("n'importe quoi %%% [[[", (CodeLanguage)99).Ok);
        }
    }
}
