using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.ExtensibleStorage;
namespace AstraBimAdapter
{
    public static class Actions
    {
        static string S(JsonObject o, string k, string fallback = "")
        {
            return o[k]?.GetValue<string>() ?? fallback;
        }
        static double N(JsonObject o, string k)
        {
            double v = o[k]?.GetValue<double>() ?? throw new ArgumentException(k + " required");
            if (!double.IsFinite(v))
                throw new ArgumentException(k + " must be finite");
            return v;
        }
        static double Ft(double m)
        {
            return UnitUtils.ConvertToInternalUnits(m, UnitTypeId.Meters);
        }
        static double M(double f)
        {
            return UnitUtils.ConvertFromInternalUnits(f, UnitTypeId.Meters);
        }
        static XYZ Point(JsonNode n)
        {
            var a = n.AsArray();
            if (a.Count != 3)
                throw new ArgumentException("xyz needs 3 values");
            return new XYZ(Ft(a[0].GetValue<double>()), Ft(a[1].GetValue<double>()), Ft(a[2].GetValue<double>()));
        }
        static JsonArray J(XYZ p)
        {
            return new JsonArray(M(p.X), M(p.Y), M(p.Z));
        }
        static Element E(Document d, JsonNode n)
        {
            if (n == null)
                throw new ArgumentException("identifier required");
            var s = n.ToString();
            return (long.TryParse(s, out var i) ? d.GetElement(new ElementId(i)) : d.GetElement(s)) ?? throw new ArgumentException("Element missing: " + s);
        }
        static Guid sid = new Guid("aaac585c-99a7-4833-8be2-48ed4efcd375");
        static Schema Schema()
        {
            var s = Autodesk.Revit.DB.ExtensibleStorage.Schema.Lookup(sid);
            if (s != null)
                return s;
            var b = new SchemaBuilder(sid);
            b.SetSchemaName("AstraSourceIdentity");
            b.SetReadAccessLevel(AccessLevel.Public);
            b.SetWriteAccessLevel(AccessLevel.Public);
            b.AddSimpleField("source_id", typeof(string));
            return b.Finish();
        }
        static string Source(Element e)
        {
            var s = Autodesk.Revit.DB.ExtensibleStorage.Schema.Lookup(sid);
            if (s == null)
                return "";
            var a = e.GetEntity(s);
            return a.IsValid() ? a.Get<string>(s.GetField("source_id")) : "";
        }
        static void SetSource(Element e, string source)
        {
            var s = Schema();
            var a = new Entity(s);
            a.Set(s.GetField("source_id"), source);
            e.SetEntity(a);
        }
        static void Set(Element e, BuiltInParameter p, double v)
        {
            var a = e.get_Parameter(p);
            if (a == null || a.IsReadOnly)
                throw new ArgumentException("Parameter unavailable: " + p);
            a.Set(v);
        }
        static void SetText(Element e, BuiltInParameter p, string v)
        {
            var a = e.get_Parameter(p);
            if (a == null || a.IsReadOnly)
                throw new ArgumentException("Parameter unavailable: " + p);
            a.Set(v);
        }
        public static JsonObject Describe(Document d, JsonObject a)
        {
            return DescribeElement(E(d, a["id"]));
        }
        static JsonObject DescribeElement(Element e)
        {
            var r = new JsonObject { ["id"] = e.Id.Value, ["unique_id"] = e.UniqueId, ["name"] = e.Name, ["category"] = e.Category?.Name, ["class"] = e.GetType().Name, ["source_id"] = Source(e) };
            var box = e.get_BoundingBox(null);
            if (box != null)
                r["bounds_m"] = new JsonObject { ["min"] = J(box.Min), ["max"] = J(box.Max) };
            if (e.Location is LocationPoint lp)
                r["point_m"] = J(lp.Point);
            if (e.Location is LocationCurve lc && lc.Curve != null)
                r["curve_m"] = new JsonArray(J(lc.Curve.GetEndPoint(0)), J(lc.Curve.GetEndPoint(1)));
            if (e is Grid g)
                r["curve_m"] = new JsonArray(J(g.Curve.GetEndPoint(0)), J(g.Curve.GetEndPoint(1)));
            if (e is FamilySymbol fs)
            {
                r["family"] = fs.FamilyName;
                r["placement"] = fs.Family.FamilyPlacementType.ToString();
                var dims = new JsonArray();
                foreach (Parameter p in fs.Parameters)
                    if (p.StorageType == StorageType.Double)
                        dims.Add(new JsonObject { ["name"] = p.Definition.Name, ["value_m"] = M(p.AsDouble()), ["readonly"] = p.IsReadOnly });
                r["dimensions"] = dims;
            }
            if (e is FamilyInstance fi)
            {
                r["type_id"] = fi.Symbol.Id.Value;
                r["host_id"] = fi.Host?.Id.Value;
                r["hand_flipped"] = fi.HandFlipped;
                r["facing_flipped"] = fi.FacingFlipped;
                r["facing_vector"] = new JsonArray(fi.FacingOrientation.X, fi.FacingOrientation.Y, fi.FacingOrientation.Z);
                r["hand_vector"] = new JsonArray(fi.HandOrientation.X, fi.HandOrientation.Y, fi.HandOrientation.Z);
            }
            if (e is WallType wt)
            {
                r["width_m"] = M(wt.Width);
                r["wall_kind"] = wt.Kind.ToString();
            }
            if (e is Room room)
            {
                r["number"] = room.Number;
                r["area_m2"] = UnitUtils.ConvertFromInternalUnits(room.Area, UnitTypeId.SquareMeters);
                r["bounded"] = room.Area > 0;
                r["level_id"] = room.LevelId.Value;
                var dependencies = new JsonArray();
                foreach (var dependentId in room.GetDependentElements(null))
                {
                    var dependent = e.Document.GetElement(dependentId);
                    dependencies.Add(new JsonObject { ["id"] = dependentId.Value, ["unique_id"] = dependent.UniqueId, ["class"] = dependent.GetType().Name });
                }
                r["dependencies"] = dependencies;
            }
            if (e is ViewPlan v)
            {
                r["level_id"] = v.GenLevel?.Id.Value;
                r["is_template"] = v.IsTemplate;
                r["scale"] = v.Scale;
            }
            if (e is ImportInstance imp)
            {
                var t = imp.GetTotalTransform();
                r["origin_m"] = J(t.Origin);
                r["basis_x"] = new JsonArray(t.BasisX.X, t.BasisX.Y, t.BasisX.Z);
                r["basis_y"] = new JsonArray(t.BasisY.X, t.BasisY.Y, t.BasisY.Z);
            }
            var parameters = new JsonArray();
            foreach (Parameter p in e.Parameters)
            {
                if (!p.HasValue)
                    continue;
                string name = p.Definition.Name;
                if (new[] { "Largeur", "Hauteur", "Width", "Height", "Appui", "Allège", "Sill Height", "Hauteur de l'appui", "Repère", "Mark" }.Any(x => name.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
                    parameters.Add(new JsonObject { ["name"] = name, ["value"] = p.StorageType == StorageType.Double ? JsonValue.Create(M(p.AsDouble())) : JsonValue.Create(p.AsValueString() ?? p.AsString()), ["readonly"] = p.IsReadOnly });
            }
            if (parameters.Count > 0)
                r["parameters"] = parameters;
            return r;
        }
        public static JsonObject ProjectInfo(Document d, JsonObject a)
        {
            if (a["save_document"]?.GetValue<bool>() == true)
            {
                throw new ArgumentException("Use the guarded save_project tool");
            }
            var r = new JsonObject { ["success"] = true, ["name"] = d.ProjectInformation.Name, ["number"] = d.ProjectInformation.Number, ["path"] = d.PathName, ["units"] = "meters", ["active_view"] = DescribeElement(d.ActiveView) };
            if (a["export_view"]?.GetValue<bool>() == true)
            {
                string dir;
                // The adapter is loaded from bytes; keep exports in a user-writable application folder.
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DreamsignRevitMCP", "exports");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "view-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                var o = new ImageExportOptions { FilePath = path, ExportRange = ExportRange.CurrentView, HLRandWFViewsFileType = ImageFileType.PNG, ShadowViewsFileType = ImageFileType.PNG, ZoomType = ZoomFitType.FitToPage, PixelSize = 2400 };
                d.ExportImage(o);
                r["image_prefix"] = path;
            }
            if (a["save_document"]?.GetValue<bool>() == true)
                r["saved"] = true;
            return r;
        }
        public static JsonObject Create(Document d, JsonObject s)
        {
            var kind = S(s, "kind");
            var source = S(s, "source_id");
            if (source.Length == 0 || source.Length > 128)
                throw new ArgumentException("source_id required");
            var matches = new FilteredElementCollector(d).WherePasses(new ExtensibleStorageFilter(sid)).Where(e => Source(e) == source).ToList();
            if (matches.Count > 1)
                throw new ArgumentException("Duplicate source_id");
            var prior = matches.SingleOrDefault();
            if (prior != null)
            {
                var types = new Dictionary<string, Type> { { "furniture_solid", typeof(DirectShape) }, { "grid", typeof(Grid) }, { "component", typeof(FamilyInstance) }, { "curtain_opening", typeof(Element) }, { "wall", typeof(Wall) }, { "wall_type", typeof(WallType) }, { "family_type", typeof(FamilySymbol) }, { "door", typeof(FamilyInstance) }, { "window", typeof(FamilyInstance) }, { "room", typeof(Room) }, { "room_tag", typeof(RoomTag) }, { "element_tag", typeof(IndependentTag) }, { "room_boundary", typeof(ModelCurve) }, { "load_family", typeof(Family) } };
                if (!types.ContainsKey(kind) || !types[kind].IsInstanceOfType(prior))
                    throw new ArgumentException("source_id kind conflict");
                // Reconstruction phases use deterministic source IDs. Existing objects are preserved;
                // changes to geometry require explicit modification rather than silent duplication.
                var r = DescribeElement(prior);
                r["success"] = true;
                r["status"] = "unchanged";
                return r;
            }
            Element result;
            switch (kind)
            {
                case "furniture_solid":
                    {
                        var pts = s["points_m"].AsArray().Select(Point).ToList();
                        var loop = new CurveLoop();
                        for (int i = 0; i < pts.Count; i++)
                            loop.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));
                        var solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, Ft(N(s, "height_m")));
                        var shape = DirectShape.CreateElement(d, new ElementId(BuiltInCategory.OST_Furniture));
                        shape.SetShape(new List<GeometryObject> { solid });
                        shape.Name = S(s, "name");
                        SetText(shape, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, "Mobilier simplifié : emprise DWG, hauteur conventionnelle.");
                        result = shape;
                        break;
                    }
                case "grid":
                    {
                        var grid = Grid.Create(d, Line.CreateBound(Point(s["start_m"]), Point(s["end_m"])));
                        grid.Name = S(s, "name");
                        d.Regenerate();
                        if (s["view_id"] != null)
                        {
                            var v = (View)E(d, s["view_id"]);
                            grid.ShowBubbleInView(DatumEnds.End0, v);
                            grid.ShowBubbleInView(DatumEnds.End1, v);
                        }
                        result = grid;
                        break;
                    }
                case "component":
                    {
                        var sym = (FamilySymbol)E(d, s["type_id"]);
                        var level = (Level)E(d, s["level_id"]);
                        if (sym.Family.FamilyPlacementType != FamilyPlacementType.OneLevelBased)
                            throw new ArgumentException("Free level-based family required");
                        if (!sym.IsActive)
                        {
                            sym.Activate();
                            d.Regenerate();
                        }
                        var p = Point(s["point_m"]);
                        var fi = d.Create.NewFamilyInstance(p, sym, level, StructuralType.NonStructural);
                        d.Regenerate();
                        double a = (s["rotation_deg"]?.GetValue<double>() ?? 0) * Math.PI / 180;
                        if (Math.Abs(a) > 1e-9)
                            ElementTransformUtils.RotateElement(d, fi.Id, Line.CreateBound(p, p + XYZ.BasisZ), a);
                        d.Regenerate();
                        if (s["center_xy"]?.GetValue<bool>() == true)
                        {
                            var box = fi.get_BoundingBox(null);
                            var middle = (box.Min + box.Max) / 2;
                            ElementTransformUtils.MoveElement(d, fi.Id, new XYZ(p.X - middle.X, p.Y - middle.Y, 0));
                        }
                        SetText(fi, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, S(s, "description"));
                        result = fi;
                        break;
                    }
                case "curtain_opening":
                    {
                        var host = (Wall)E(d, s["host_id"]);
                        var level = (Level)E(d, s["level_id"]);
                        var type = (WallType)E(d, s["type_id"]);
                        if (type.Kind != WallKind.Curtain)
                            throw new ArgumentException("Curtain type required");
                        var p = Point(s["point_m"]);
                        var axis = ((LocationCurve)host.Location).Curve;
                        var u = (axis.GetEndPoint(1) - axis.GetEndPoint(0)).Normalize();
                        double half = Ft(N(s, "width_m")) / 2, h = Ft(N(s, "height_m"));
                        var a = p - u * half;
                        var b = p + u * half;
                        var opening = d.Create.NewOpening(host, a, b + XYZ.BasisZ * h);
                        SetSource(opening, source + "-void");
                        var cw = Wall.Create(d, Line.CreateBound(a, b), type.Id, level.Id, h, p.Z - level.Elevation, false, false);
                        WallUtils.DisallowWallJoinAtEnd(cw, 0);
                        WallUtils.DisallowWallJoinAtEnd(cw, 1);
                        SetSource(cw, source + "-frame");
                        d.Regenerate();
                        var panels = cw.CurtainGrid.GetPanelIds();
                        if (panels.Count != 1)
                            throw new ArgumentException("Expected one curtain panel");
                        var panel = d.GetElement(panels.Single());
                        if (s["door_type_id"] != null)
                        {
                            var symbol = (FamilySymbol)E(d, s["door_type_id"]);
                            if (!symbol.IsActive)
                                symbol.Activate();
                            d.Regenerate();
                            var changed = panel.ChangeTypeId(symbol.Id);
                            if (changed != ElementId.InvalidElementId)
                                panel = d.GetElement(changed);
                        }
                        if (s["mark"] != null)
                            SetText(panel, BuiltInParameter.ALL_MODEL_MARK, S(s, "mark"));
                        result = panel;
                        break;
                    }
                case "view_setup":
                    {
                        var view = (ViewPlan)E(d, s["view_id"]);
                        if (view.Id != d.ActiveView.Id)
                            throw new ArgumentException("Only active view can be configured");
                        if (s["normal_display"]?.GetValue<bool>() == true)
                        {
                            if (view.IsInTemporaryViewMode(TemporaryViewMode.RevealHiddenElements))
                                view.DisableTemporaryViewMode(TemporaryViewMode.RevealHiddenElements);
                            view.CropBoxActive = true;
                            var ac = view.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);
                            if (ac != null && !ac.IsReadOnly)
                                ac.Set(1);
                        }
                        if (s["hide_ids"] != null)
                        {
                            var ids = s["hide_ids"].AsArray().Select(n => E(d, n)).Where(e => e.CanBeHidden(view) && !e.IsHidden(view)).Select(e => e.Id).ToList();
                            if (ids.Count > 0)
                                view.HideElements(ids);
                        }
                        if (s["crop_min_m"] != null)
                        {
                            var crop = view.CropBox;
                            crop.Min = crop.Transform.Inverse.OfPoint(Point(s["crop_min_m"]));
                            crop.Max = crop.Transform.Inverse.OfPoint(Point(s["crop_max_m"]));
                            view.CropBox = crop;
                            view.CropBoxActive = true;
                            view.CropBoxVisible = false;
                        }
                        if (s["scale"] != null)
                            view.Scale = s["scale"].GetValue<int>();
                        if (s["cut_height_m"] != null)
                        {
                            var range = view.GetViewRange();
                            foreach (var p in new[] { PlanViewPlane.TopClipPlane, PlanViewPlane.CutPlane, PlanViewPlane.BottomClipPlane, PlanViewPlane.ViewDepthPlane })
                                range.SetLevelId(p, view.GenLevel.Id);
                            range.SetOffset(PlanViewPlane.TopClipPlane, Ft(N(s, "top_height_m")));
                            range.SetOffset(PlanViewPlane.CutPlane, Ft(N(s, "cut_height_m")));
                            range.SetOffset(PlanViewPlane.BottomClipPlane, 0);
                            range.SetOffset(PlanViewPlane.ViewDepthPlane, Ft(-.1));
                            view.SetViewRange(range);
                        }
                        return new JsonObject { ["success"] = true, ["status"] = "modified", ["view_id"] = view.Id.Value };
                    }
                case "wall_type":
                    {
                        string name = S(s, "name");
                        var existing = new FilteredElementCollector(d).OfClass(typeof(WallType)).Cast<WallType>().Where(x => x.Name == name).ToList();
                        if (existing.Count > 0)
                            throw new ArgumentException("Type name already exists; use existing ID");
                        var seed = (WallType)E(d, s["base_type_id"]);
                        if (seed.Kind != WallKind.Basic)
                            throw new ArgumentException("Basic wall required");
                        var wt = (WallType)seed.Duplicate(name);
                        double width = Ft(N(s, "width_m"));
                        if (width < Ft(.05) || width > Ft(1))
                            throw new ArgumentException("Wall width outside supported range");
                        var cs = CompoundStructure.CreateSimpleCompoundStructure(new List<CompoundStructureLayer> { new CompoundStructureLayer(width, MaterialFunctionAssignment.Structure, ElementId.InvalidElementId) });
                        wt.SetCompoundStructure(cs);
                        result = wt;
                        break;
                    }
                case "wall":
                    {
                        var type = (WallType)E(d, s["type_id"]);
                        var level = (Level)E(d, s["level_id"]);
                        var start = Point(s["start_m"]);
                        var end = Point(s["end_m"]);
                        double height = N(s, "height_m");
                        if (height <= 0 || height > 30)
                            throw new ArgumentException("Invalid height");
                        var wall = Wall.Create(d, Line.CreateBound(start, end), type.Id, level.Id, Ft(height), start.Z - level.Elevation, false, false);
                        if (s["top_level_id"] != null)
                        {
                            wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(E(d, s["top_level_id"]).Id);
                        }
                        result = wall;
                        break;
                    }
                case "family_type":
                    {
                        var seed = (FamilySymbol)E(d, s["base_type_id"]);
                        var name = S(s, "name");
                        if (seed.Family.GetFamilySymbolIds().Select(x => d.GetElement(x)).Any(x => x.Name == name))
                            throw new ArgumentException("Type name exists; use existing ID");
                        var type = (FamilySymbol)seed.Duplicate(name);
                        foreach (var key in new[] { "width_m", "height_m" })
                            if (s[key] != null)
                            {
                                var bi = key == "width_m" ? BuiltInParameter.FAMILY_WIDTH_PARAM : BuiltInParameter.FAMILY_HEIGHT_PARAM;
                                var p = type.get_Parameter(bi);
                                if (p == null || p.IsReadOnly)
                                {
                                    var pname = S(s, key == "width_m" ? "width_parameter" : "height_parameter");
                                    p = type.LookupParameter(pname);
                                }
                                if (p == null || p.IsReadOnly || p.StorageType != StorageType.Double)
                                    throw new ArgumentException("Writable dimension missing: " + key);
                                p.Set(Ft(N(s, key)));
                            }
                        if (s["dimensions"] != null)
                            foreach (var n in s["dimensions"].AsArray())
                            {
                                var entry = n.AsObject();
                                var p = type.LookupParameter(S(entry, "name"));
                                if (p == null || p.IsReadOnly || p.StorageType != StorageType.Double)
                                    throw new ArgumentException("Dimension unavailable: " + S(entry, "name"));
                                p.Set(Ft(N(entry, "value_m")));
                            }
                        result = type;
                        break;
                    }
                case "door":
                case "window":
                    {
                        var symbol = (FamilySymbol)E(d, s["type_id"]);
                        var host = (Wall)E(d, s["host_id"]);
                        var level = (Level)E(d, s["level_id"]);
                        if (symbol.Category.Id.Value != (long)(kind == "door" ? BuiltInCategory.OST_Doors : BuiltInCategory.OST_Windows))
                            throw new ArgumentException("Category mismatch");
                        if (!symbol.IsActive)
                        {
                            symbol.Activate();
                            d.Regenerate();
                        }
                        var p = Point(s["point_m"]);
                        var curve = ((LocationCurve)host.Location).Curve;
                        var projection = curve.Project(p);
                        if (projection == null || Math.Abs(projection.XYZPoint.X - p.X) + Math.Abs(projection.XYZPoint.Y - p.Y) > Ft(.1))
                            throw new ArgumentException("Opening point not on host axis");
                        if (s["unjoin_wall_ids"] != null)
                        {
                            foreach (var n in s["unjoin_wall_ids"].AsArray())
                            {
                                var neighbor = (Wall)E(d, n);
                                var axis = ((LocationCurve)neighbor.Location).Curve;
                                for (int i = 0; i < 2; i++)
                                {
                                    var ep = axis.GetEndPoint(i);
                                    if (new XYZ(ep.X - p.X, ep.Y - p.Y, 0).GetLength() < Ft(.7))
                                        WallUtils.DisallowWallJoinAtEnd(neighbor, i);
                                }
                            }
                            d.Regenerate();
                        }
                        var instance = d.Create.NewFamilyInstance(p, symbol, host, level, StructuralType.NonStructural);
                        if (s["sill_m"] != null)
                            Set(instance, BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM, Ft(N(s, "sill_m")));
                        if (s["hand_flipped"]?.GetValue<bool>() == true && instance.CanFlipHand)
                            instance.flipHand();
                        if (s["facing_flipped"]?.GetValue<bool>() == true && instance.CanFlipFacing)
                            instance.flipFacing();
                        if (s["facing_vector"] != null)
                        {
                            var v = s["facing_vector"].AsArray();
                            var desired = new XYZ(v[0].GetValue<double>(), v[1].GetValue<double>(), 0);
                            if (instance.FacingOrientation.DotProduct(desired) < 0 && instance.CanFlipFacing)
                                instance.flipFacing();
                        }
                        if (s["hand_vector"] != null)
                        {
                            var v = s["hand_vector"].AsArray();
                            var desired = new XYZ(v[0].GetValue<double>(), v[1].GetValue<double>(), 0);
                            if (instance.HandOrientation.DotProduct(desired) < 0 && instance.CanFlipHand)
                                instance.flipHand();
                        }
                        if (s["mark"] != null)
                            SetText(instance, BuiltInParameter.ALL_MODEL_MARK, S(s, "mark"));
                        result = instance;
                        break;
                    }
                case "room":
                    {
                        var level = (Level)E(d, s["level_id"]);
                        var p = Point(s["point_m"]);
                        var room = d.Create.NewRoom(level, new UV(p.X, p.Y));
                        room.Name = S(s, "name");
                        room.Number = S(s, "number");
                        if (s["height_m"] != null)
                            Set(room, BuiltInParameter.ROOM_UPPER_OFFSET, Ft(N(s, "height_m")));
                        d.Regenerate();
                        if (room.Area <= 0)
                            throw new ArgumentException("Room is not enclosed at supplied point");
                        result = room;
                        break;
                    }
                case "room_tag":
                    {
                        var room = (Room)E(d, s["room_id"]);
                        var view = (ViewPlan)E(d, s["view_id"]);
                        if (view.GenLevel.Id != room.LevelId)
                            throw new ArgumentException("Tag view level mismatch");
                        var p = Point(s["point_m"]);
                        var tag = d.Create.NewRoomTag(new LinkElementId(room.Id), new UV(p.X, p.Y), view.Id);
                        if (s["type_id"] != null)
                            tag.ChangeTypeId(E(d, s["type_id"]).Id);
                        result = tag;
                        break;
                    }
                case "element_tag":
                    {
                        var e = E(d, s["element_id"]);
                        var view = (View)E(d, s["view_id"]);
                        var tag = IndependentTag.Create(d, E(d, s["type_id"]).Id, view.Id, new Reference(e), false, TagOrientation.Horizontal, Point(s["point_m"]));
                        result = tag;
                        break;
                    }
                case "room_boundary":
                    {
                        var view = (ViewPlan)E(d, s["view_id"]);
                        var start = Point(s["start_m"]);
                        var end = Point(s["end_m"]);
                        var sketch = SketchPlane.Create(d, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, view.GenLevel.Elevation)));
                        var curves = new CurveArray();
                        curves.Append(Line.CreateBound(start, end));
                        result = d.Create.NewRoomBoundaryLines(sketch, curves, view).get_Item(0);
                        break;
                    }
                case "load_family":
                    {
                        var path = Path.GetFullPath(S(s, "path"));
                        var root = Path.GetFullPath(Environment.GetEnvironmentVariable("ASTRA_REVIT_FAMILY_ROOT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Autodesk", "RVT 2027", "Libraries", "French")) + Path.DirectorySeparatorChar;
                        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !path.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                            throw new ArgumentException("Family must be an existing RFA inside the configured family library");
                        if (!d.LoadFamily(path, out Family family))
                            throw new ArgumentException("Family not loaded (possibly already present); query existing types");
                        result = family;
                        break;
                    }
                default:
                    throw new ArgumentException("Unsupported BIM kind");
            }
            SetSource(result, source);
            var info = DescribeElement(result);
            info["success"] = true;
            info["status"] = "created";
            return info;
        }
    }
}
