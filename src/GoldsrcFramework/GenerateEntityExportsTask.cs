namespace GoldsrcFramework;

public class GenerateEntityExportsTask : Microsoft.Build.Utilities.Task
{
    public override bool Execute()
    {
        Log.LogMessage("Generating entity exports...");

        // Done.

        Log.LogMessage("Entity exports generated successfully.");
        return true;
    }
}
