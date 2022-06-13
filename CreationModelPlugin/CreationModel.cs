using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
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

            DrawRectangularOutlineWalls(doc, 10000, 5000, level1, level2);

            return Result.Succeeded;
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

            List<XYZ> points = GetRectangularPoints (lengthInMm, widthInMm);

            List<Wall> walls = new List<Wall>();

            Transaction transaction = new Transaction(document, "Построение стен");
            transaction.Start();

            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(document, line, baseLevel.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(upperLevel.Id);
            }

            transaction.Commit();

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
