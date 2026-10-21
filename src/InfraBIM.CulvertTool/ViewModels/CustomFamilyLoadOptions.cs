using Autodesk.Revit.DB;

namespace InfraBIM.CulvertTool.ViewModels;

public class CustomFamilyLoadOptions : IFamilyLoadOptions
{
	public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
	{
		overwriteParameterValues = true;
		return true;
	}

	public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
	{
		source = (FamilySource)1;
		overwriteParameterValues = true;
		return true;
	}
}
