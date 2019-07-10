using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using Autodesk.Revit.DB;
using Grasshopper.Kernel.Special;

namespace RhinoInside.Revit.GH.Parameters
{
  public class DirectShapeCategories : GH_ValueList
  {
    public override Guid ComponentGuid => new Guid("7BAFE137-332B-481A-BE22-09E8BD4C86FC");
    public override GH_Exposure Exposure => GH_Exposure.secondary;

    public DirectShapeCategories()
    {
      Category = "Revit";
      SubCategory = "Build";
      Name = "DirectShape.Categories";
      NickName = "Categories";
      Description = "Provides a picker of a valid DirectShape category";

      ListItems.Clear();

      var ActiveDBDocument = Revit.ActiveDBDocument;
      if (ActiveDBDocument == null)
        return;

      var genericModel = Autodesk.Revit.DB.Category.GetCategory(ActiveDBDocument, BuiltInCategory.OST_GenericModel);

      var directShapeCategories = ActiveDBDocument.Settings.Categories.Cast<Autodesk.Revit.DB.Category>().Where((x) => DirectShape.IsValidCategoryId(x.Id, ActiveDBDocument));
      foreach (var group in directShapeCategories.GroupBy((x) => x.CategoryType).OrderBy((x) => x.Key))
      {
        foreach (var category in group.OrderBy(x => x.Name))
        {
          ListItems.Add(new GH_ValueListItem(category.Name, category.Id.IntegerValue.ToString()));
          if (category.Id.IntegerValue == (int) BuiltInCategory.OST_GenericModel)
            SelectItem(ListItems.Count - 1);
        }
      }
    }
  }
}

namespace RhinoInside.Revit.GH.Components
{
  public class DirectShapeByGeometry : GH_TransactionalComponentItem
  {
    public override Guid ComponentGuid => new Guid("0bfbda45-49cc-4ac6-8d6d-ecd2cfed062a");
    public override GH_Exposure Exposure => GH_Exposure.secondary;

    public DirectShapeByGeometry() : base
    (
      "AddDirectShape.ByGeometry", "ByGeometry",
      "Given its Geometry, it adds a DirectShape element to the active Revit document",
      "Revit", "Build"
    )
    { }

    protected override void RegisterInputParams(GH_InputParamManager manager)
    {
      manager.AddGeometryParameter("Geometry", "G", string.Empty, GH_ParamAccess.list);
      manager[manager.AddParameter(new Parameters.Category(), "Category", "C", string.Empty, GH_ParamAccess.item)].Optional = true;
      manager[manager.AddTextParameter("Name", "N", string.Empty, GH_ParamAccess.item)].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager manager)
    {
      manager.AddParameter(new Parameters.Element(), "DirectShape", "DS", "New DirectShape", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      var geometry = new List<IGH_GeometricGoo>();
      DA.GetDataList("Geometry", geometry);

      Autodesk.Revit.DB.Category category = null;
      if (!DA.GetData("Category", ref category) && Params.Input[1].Sources.Count == 0)
        category = Autodesk.Revit.DB.Category.GetCategory(Revit.ActiveDBDocument, BuiltInCategory.OST_GenericModel);

      string name = null;
      if (!DA.GetData("Name", ref name) && geometry.Count == 1 && (geometry[0]?.IsReferencedGeometry ?? false))
        name = Rhino.RhinoDoc.ActiveDoc.Objects.FindId(geometry[0].ReferenceID)?.Name;

      DA.DisableGapLogic();
      int Iteration = DA.Iteration;
      Revit.EnqueueAction((doc) => CommitInstance(doc, DA, Iteration, geometry, category, name));
    }

    Rhino.Geometry.GeometryBase AsGeometryBase(IGH_GeometricGoo obj)
    {
      var scriptVariable = obj.ScriptVariable();
      switch (scriptVariable)
      {
        case Rhino.Geometry.Point3d point:    return new Rhino.Geometry.Point(point);
        case Rhino.Geometry.Line line:        return new Rhino.Geometry.LineCurve(line);
        case Rhino.Geometry.Rectangle3d rect: return rect.ToNurbsCurve();
        case Rhino.Geometry.Arc arc:          return new Rhino.Geometry.ArcCurve(arc);
        case Rhino.Geometry.Circle circle:    return new Rhino.Geometry.ArcCurve(circle);
        case Rhino.Geometry.Ellipse ellipse:  return ellipse.ToNurbsCurve();
        case Rhino.Geometry.Box box:          return box.ToBrep();
      }

      return scriptVariable as Rhino.Geometry.GeometryBase;
    }

    void CommitInstance
    (
      Document doc, IGH_DataAccess DA, int Iteration,
      IEnumerable<IGH_GeometricGoo> geometries,
      Autodesk.Revit.DB.Category category,
      string name
    )
    {
      var element = PreviousElement(doc, Iteration);
      if (!element?.Pinned ?? false)
      {
        ReplaceElement(doc, DA, Iteration, element);
      }
      else try
      {
        var shape = new List<GeometryObject>();

        if (geometries != null)
        {
          if (category == null || !DirectShape.IsValidCategoryId(category.Id, doc))
          {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, string.Format("Parameter '{0}' is not valid for DirectShape.", Params.Input[1].Name));
            category = Autodesk.Revit.DB.Category.GetCategory(doc, BuiltInCategory.OST_GenericModel);
          }

          foreach (var geometry in geometries.Select((x) => AsGeometryBase(x)).ToHost())
          {
            // DirectShape only accepts those types and no nulls
            foreach (var g in geometry.SelectMany(g => g.ToDirectShapeGeometry()))
            {
              switch (g)
              {
                case Point p: shape.Add(p); break;
                case Curve c: shape.Add(c); break;
                case Solid s: shape.Add(s); break;
                case Mesh  m: shape.Add(m); break;
              }
            }
          }
        }

        if (element?.Category.Id != category.Id)
          element = null;

        var ds = element as DirectShape ?? CopyParametersFrom(DirectShape.CreateElement(doc, category.Id), element);
        ds.SetShape(shape);
        ds.Name = name ?? string.Empty;
        element = ds;

        ReplaceElement(doc, DA, Iteration, element);
      }
      catch (Exception e)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
        ReplaceElement(doc, DA, Iteration, null);
      }
    }
  }
}
