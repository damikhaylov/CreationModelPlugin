﻿using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            Level level1;
            Level level2;

            GetTwoLowerLevels(doc, "Уровень", out level1, out level2);

            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();

            List<Wall> walls = DrawRectangularOutlineWalls(doc, 10000, 5000, level1, level2);

            for (int i = 0; i < walls.Count; i++)
            {
                AddDoorOrWindow(doc, level1, walls[i], i == 0);
            }

            AddRoof(doc, level2, walls, 1500);

            transaction.Commit();

            return Result.Succeeded;
        }

        private void AddRoof(Document document, Level level, List<Wall> walls, double rootHeightInMm)
        {
            RoofType roofType = new FilteredElementCollector(document)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            double roofHeight = UnitUtils.ConvertToInternalUnits(rootHeightInMm, UnitTypeId.Millimeters);
            double roofThickness = roofType.get_Parameter(BuiltInParameter.ROOF_ATTR_DEFAULT_THICKNESS_PARAM).AsDouble();

            double dt = walls[0].Width / 2;
            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dt, -dt, 0));
            points.Add(new XYZ(dt, -dt, 0));
            points.Add(new XYZ(dt, dt, 0));
            points.Add(new XYZ(-dt, dt, 0));

            LocationCurve roofExtrusionDirection = walls[0].Location as LocationCurve;
            double extrusionStart = roofExtrusionDirection.Curve.GetEndPoint(0).X - dt;
            double extrusionEnd = roofExtrusionDirection.Curve.GetEndPoint(1).X + dt;

            LocationCurve roofProfileCurve = walls[1].Location as LocationCurve;
            double roofAngle = Math.Atan2(roofHeight, roofProfileCurve.Curve.Length / 2);
            double roofElevation = roofThickness / Math.Cos(roofAngle);

            XYZ roofProfileCorner1 = roofProfileCurve.Curve.GetEndPoint(0);
            roofProfileCorner1 = new XYZ ( roofProfileCorner1.X, 
                roofProfileCorner1.Y - dt, 
                roofProfileCorner1.Z + level.Elevation + roofElevation);
            XYZ roofProfileCorner2 = roofProfileCurve.Curve.GetEndPoint(1);
            roofProfileCorner2 = new XYZ(roofProfileCorner2.X, 
                roofProfileCorner2.Y + dt, 
                roofProfileCorner2.Z + level.Elevation + roofElevation);
            XYZ roofProfileRidgePoint = (roofProfileCorner1 + roofProfileCorner2) / 2;
            roofProfileRidgePoint = new XYZ(roofProfileRidgePoint.X, roofProfileRidgePoint.Y,
                roofProfileRidgePoint.Z + roofHeight);

            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(roofProfileCorner1, roofProfileRidgePoint));
            curveArray.Append(Line.CreateBound(roofProfileRidgePoint, roofProfileCorner2));

            ReferencePlane plane = document.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 1), new XYZ(0, 1, 0), document.ActiveView);
            document.Create.NewExtrusionRoof(curveArray, plane, level, roofType, extrusionStart, extrusionEnd);
        }

        private void AddDoorOrWindow(Document document, Level level, Wall wall, bool isDoor)
        {
            BuiltInCategory category = isDoor ? BuiltInCategory.OST_Doors : BuiltInCategory.OST_Windows;
            string name = isDoor ? "0915 x 2134 мм" : "0915 x 1220 мм";
            string familyName = isDoor ? "Одиночные-Щитовые" : "Фиксированные";
            double sillHeight = isDoor ? 0 : UnitUtils.ConvertToInternalUnits(800, UnitTypeId.Millimeters);

            FamilySymbol type = new FilteredElementCollector(document)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(category)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals(name))
                .Where(x => x.FamilyName.Equals(familyName))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point = (hostCurve.Curve.GetEndPoint(0) + hostCurve.Curve.GetEndPoint(1)) / 2;

            if (!type.IsActive)
                type.Activate();

            FamilyInstance opening = document.Create.NewFamilyInstance(point, type, wall, level, StructuralType.NonStructural);
            opening.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(sillHeight);
        }

        private static void GetTwoLowerLevels(Document document, string levelBaseName,
            out Level level1, out Level level2)
        {
            List<Level> levels = new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();

            level1 = levels
                .Where(el => el.Name == $"{levelBaseName} 1")
                .FirstOrDefault();

            level2 = levels
                .Where(el => el.Name == $"{levelBaseName} 2")
                .FirstOrDefault();
        }

        private static List<Wall> DrawRectangularOutlineWalls(Document document, double lengthInMm, double widthInMm,
            Level baseLevel, Level upperLevel)
        {

            List<XYZ> points = GetRectangularPoints(lengthInMm, widthInMm);

            List<Wall> walls = new List<Wall>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(document, line, baseLevel.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(upperLevel.Id);
            }

            return walls;
        }

        private static List<XYZ> GetRectangularPoints(double lengthInMm, double widthInMm)
        {
            double length = UnitUtils.ConvertToInternalUnits(lengthInMm, UnitTypeId.Millimeters);
            double width = UnitUtils.ConvertToInternalUnits(widthInMm, UnitTypeId.Millimeters);
            double dx = length / 2;
            double dy = width / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            return points;
        }
    }
}
