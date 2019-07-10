using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Grasshopper.Kernel;

using Autodesk.Revit.DB;

namespace RhinoInside.Revit.GH.Components
{
  public class ModelLineByCurve : GH_TransactionalComponentList
  {
    public override Guid ComponentGuid => new Guid("240127B1-94EE-47C9-98F8-05DE32447B01");
    public override GH_Exposure Exposure => GH_Exposure.primary;

    public ModelLineByCurve() : base
    (
      "AddModelLine.ByCurve", "ByCurve",
      "Given a Curve, it adds a Curve element to the active Revit document",
      "Revit", "Model"
    )
    { }

    protected override void RegisterInputParams(GH_InputParamManager manager)
    {
      manager.AddCurveParameter("Curve", "C", string.Empty, GH_ParamAccess.item);
      manager.AddParameter(new Parameters.SketchPlane(), "SketchPlane", "SP", "Plane where curve will be projected", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager manager)
    {
      manager.AddParameter(new Parameters.Element(), "CurveElement", "C", "New CurveElement", GH_ParamAccess.list);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      Rhino.Geometry.Curve axis = null;
      DA.GetData("Curve", ref axis);

      Autodesk.Revit.DB.SketchPlane plane = null;
      DA.GetData("SketchPlane", ref plane);

      DA.DisableGapLogic();
      int Iteration = DA.Iteration;
      Revit.EnqueueAction((doc) => CommitInstance(doc, DA, Iteration, axis, plane));
    }

    void CommitInstance
    (
      Document doc, IGH_DataAccess DA, int Iteration,
      Rhino.Geometry.Curve curve,
      Autodesk.Revit.DB.SketchPlane plane
    )
    {
      var elements = PreviousElements(doc, Iteration).ToList();
      try
      {
        if (curve == null)
          throw new Exception(string.Format("Parameter '{0}' is null.", Params.Input[0].Name));

        if (plane  == null)
          throw new Exception(string.Format("Parameter '{0}' is null.", Params.Input[1].Name));

        var scaleFactor = 1.0 / Revit.ModelUnits;
        if (scaleFactor != 1.0)
          curve.Scale(scaleFactor);

        curve = Rhino.Geometry.Curve.ProjectToPlane(curve, plane.GetPlane().ToRhino());

        var newElements = new List<Element>();
        {
          int index = 0;
          foreach (var c in curve.ToHost() ?? Enumerable.Empty<Autodesk.Revit.DB.Curve>())
          {
            var element = index < elements.Count ? elements[index] : null;
            index++;

            if (element?.Pinned ?? true)
            {
              if (element is ModelCurve modelCurve && c.IsSameKindAs(modelCurve.GeometryCurve))
                modelCurve.SetSketchPlaneAndCurve(plane, c);
              else if (doc.IsFamilyDocument)
                element = CopyParametersFrom(doc.FamilyCreate.NewModelCurve(c, plane), element);
              else
                element = CopyParametersFrom(doc.Create.NewModelCurve(c, plane), element);
            }

            newElements.Add(element);
          }
        }

        ReplaceElements(doc, DA, Iteration, newElements);
      }
      catch (Exception e)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
        ReplaceElements(doc, DA, Iteration, null);
      }
    }
  }
}

