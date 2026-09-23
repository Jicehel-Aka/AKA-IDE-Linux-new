namespace GamebuinoAKA.Core.Models
{
    /// <summary>Résultat d'une exécution de processus (sortie bufferisée).</summary>
    public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public bool Success => ExitCode == 0;
    }
}
