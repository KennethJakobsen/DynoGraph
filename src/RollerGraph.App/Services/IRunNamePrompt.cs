namespace RollerGraph.App.Services;

/// <summary>Prompts the user for a name for a saved run.</summary>
public interface IRunNamePrompt
{
    /// <summary>
    /// Asks the user for a saved-run name. Returns the name they entered, or
    /// null if they cancelled the dialog.
    /// </summary>
    /// <param name="suggested">Pre-populated suggestion (e.g. <c>Run 3</c>).</param>
    Task<string?> AskForRunNameAsync(string suggested);
}
