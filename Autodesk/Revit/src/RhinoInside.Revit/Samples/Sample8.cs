using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Rhino;
using Rhino.Geometry;
using Rhino.FileIO;
using Rhino.DocObjects;
using RhinoInside.Revit.UI;

namespace RhinoInside.Revit.Samples
{
  [Transaction(TransactionMode.Manual), Regeneration(RegenerationOption.Manual)]
  public class Sample8 : RhinoCommand
  {
    public static void CreateUI(RibbonPanel ribbonPanel)
    {
      var buttonData = NewPushButtonData<Sample8, Availability>("Sample 8");

      if (ribbonPanel.AddItem(buttonData) is PushButton pushButton)
      {
        pushButton.ToolTip = "Imports geometry from 3dm file to a Revit model or family";
        pushButton.Image = ImageBuilder.BuildImage("8");
        pushButton.LargeImage = ImageBuilder.BuildLargeImage("8");
        pushButton.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, "https://github.com/mcneel/rhino.inside/blob/master/Autodesk/Revit/README.md#sample-8"));
      }
    }

    static IList<GeometryObject> ImportObject(GeometryBase geometry, ObjectAttributes attributes, double scaleFactor)
    {
      return geometry.ToHost(scaleFactor).ToList();
    }

    public static Result Import3DMFile(string filePath, Document doc, BuiltInCategory builtInCategory)
    {
      using (var model = File3dm.Read(filePath))
      {
        var scaleFactor = RhinoMath.UnitScale(model.Settings.ModelUnitSystem, Revit.ModelUnitSystem);

        using (var trans = new Transaction(doc, "Import 3D Model"))
        {
          if (trans.Start() == TransactionStatus.Started)
          {
            var categoryId = new ElementId(builtInCategory);

            var ds = DirectShape.CreateElement(doc, categoryId);
            ds.Name = Path.GetFileName(filePath);

            foreach (var obj in model.Objects)
            {
              if (!obj.Attributes.Visible)
                continue;

              var layer = model.AllLayers.FindIndex(obj.Attributes.LayerIndex);
              if (layer?.IsVisible != true)
                continue;

              var geometryList = ImportObject(obj.Geometry, obj.Attributes, scaleFactor);
              if (geometryList == null)
                continue;

              try { ds.AppendShape(geometryList); }
              catch (Autodesk.Revit.Exceptions.ArgumentException) { }
            }

            if (trans.Commit() == TransactionStatus.Committed)
            {
              var elements = new ElementId[] { ds.Id };
              Revit.ActiveUIDocument.Selection.SetElementIds(elements);
              Revit.ActiveUIDocument.ShowElements(elements);

              return Result.Succeeded;
            }
          }
        }
      }

      return Result.Failed;
    }

    public override Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
    {
      if(!DirectShape.IsSupportedDocument(data.Application.ActiveUIDocument.Document))
      {
        message = "Active document can't support DirectShape functionality.";
        return Result.Failed;
      }

      using
      (
        var openFileDialog = new OpenFileDialog()
        {
          Filter = "Rhino 3D models (*.3dm)|*.3dm",
          FilterIndex = 1,
          RestoreDirectory = true
        }
      )
      {
        switch (openFileDialog.ShowDialog(ModalForm.OwnerWindow))
        {
          case DialogResult.OK:
            return Import3DMFile
            (
              openFileDialog.FileName,
              data.Application.ActiveUIDocument.Document,
              Sample4.ActiveBuiltInCategory
            );
          case DialogResult.Cancel: return Result.Cancelled;
        }
      }

      return Result.Failed;
    }
  }
}
