using System;
using Autodesk.Revit.DB;

namespace InfraBIM.CulvertTool.Services
{
    public static class CoordinateService
    {
        /// <summary>
        /// Chuyển đổi tọa độ thực địa VN2000 (Mét) sang tọa độ nội bộ Revit (Internal Coordinates - Feet)
        /// có bù trừ Survey Point (EastWest, NorthSouth, Elevation) và góc xoay True North (Angle).
        /// </summary>
        public static XYZ ConvertVN2000ToRevitInternal(Document doc, double xM, double yM, double zM, bool useSurveyPoint = true)
        {
            double xFeet = UnitUtils.ConvertToInternalUnits(xM, UnitTypeId.Meters);
            double yFeet = UnitUtils.ConvertToInternalUnits(yM, UnitTypeId.Meters);
            double zFeet = UnitUtils.ConvertToInternalUnits(zM, UnitTypeId.Meters);
            XYZ ptInput = new XYZ(xFeet, yFeet, zFeet);

            if (!useSurveyPoint || doc.ActiveProjectLocation == null)
            {
                return ptInput;
            }

            ProjectPosition pos = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            double angle = pos.Angle;

            double dx = ptInput.X - pos.EastWest;
            double dy = ptInput.Y - pos.NorthSouth;
            double dz = ptInput.Z - pos.Elevation;

            double internalX = dx * Math.Cos(angle) - dy * Math.Sin(angle);
            double internalY = dx * Math.Sin(angle) + dy * Math.Cos(angle);
            double internalZ = dz;

            return new XYZ(internalX, internalY, internalZ);
        }

        /// <summary>
        /// Chuyển đổi ngược từ tọa độ Revit Internal (Feet) sang VN2000 (Mét)
        /// </summary>
        public static (double X, double Y, double Z) ConvertRevitInternalToVN2000(Document doc, XYZ internalPt, bool useSurveyPoint = true)
        {
            if (!useSurveyPoint || doc.ActiveProjectLocation == null)
            {
                return (
                    UnitUtils.ConvertFromInternalUnits(internalPt.X, UnitTypeId.Meters),
                    UnitUtils.ConvertFromInternalUnits(internalPt.Y, UnitTypeId.Meters),
                    UnitUtils.ConvertFromInternalUnits(internalPt.Z, UnitTypeId.Meters)
                );
            }

            ProjectPosition pos = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            double angle = pos.Angle;

            // Xoay ngược lại (nghịch đảo của ma trận xoay góc angle)
            double dx = internalPt.X * Math.Cos(angle) + internalPt.Y * Math.Sin(angle);
            double dy = -internalPt.X * Math.Sin(angle) + internalPt.Y * Math.Cos(angle);
            double dz = internalPt.Z;

            double easting = dx + pos.EastWest;
            double northing = dy + pos.NorthSouth;
            double elevation = dz + pos.Elevation;

            return (
                UnitUtils.ConvertFromInternalUnits(easting, UnitTypeId.Meters),
                UnitUtils.ConvertFromInternalUnits(northing, UnitTypeId.Meters),
                UnitUtils.ConvertFromInternalUnits(elevation, UnitTypeId.Meters)
            );
        }
    }
}
