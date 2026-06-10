namespace RollerGraph.App.Services;

/// <summary>Asks the user to confirm a destructive action.</summary>
public interface IConfirmPrompt
{
    /// <summary>
    /// Prompt for confirmation that a saved run with the given name should be
    /// overwritten by the current data. Returns true to proceed.
    /// </summary>
    Task<bool> ConfirmOverwriteAsync(string runName);
}
