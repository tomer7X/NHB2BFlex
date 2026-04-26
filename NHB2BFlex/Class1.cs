using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NHB2BFlex
{
    public class Commands
    {
        private const string QuantityTag = "Quantity";
        private const string NameTag = "NAME";
        private const int ColumnsPerRow = 10;
        private const string IgnoredAttributePrefix = "I_";

        [CommandMethod("NHB2BFlex")]
        public void NHBlock()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n========== NHB2BFlex Command Started ==========");
            ed.WriteMessage("\n--- STEP 1: Select Front Blocks ---");
            ed.WriteMessage("\nPlease select all FRONT blocks (multiple instances of the SAME type).");
            ed.WriteMessage("\nNote: All selected blocks must be of the same block type.");

            // --- Step 1: select Front blocks, keep only block references ---
            var psr = ed.GetSelection();
            if (psr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nCancelled by user.");
                return;
            }

            var blockIds = new List<ObjectId>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var selObj in psr.Value)
                {
                    var so = (SelectedObject)selObj;
                    if (!so.ObjectId.IsValid) continue;
                    var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as BlockReference;
                    if (ent != null)
                        blockIds.Add(so.ObjectId);
                }
                tr.Commit();
            }

            if (blockIds.Count == 0)
            {
                ed.WriteMessage("\nNo block references found in selection.");
                ed.WriteMessage("\nPlease select only block references (not other objects).");
                return;
            }

            ed.WriteMessage($"\n✓ {blockIds.Count} block reference(s) selected.");

            // --- Step 2: validate that all Front blocks are of the same type ---
            string frontBlockType = null;
            var frontBlocksData = new List<SourceBlockData>();

            ed.WriteMessage("\n--- Validating Front Block Types ---");

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in blockIds)
                {
                    var br = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
                    string name = GetBlockEffectiveName(tr, br);

                    if (frontBlockType == null)
                    {
                        frontBlockType = name;
                        ed.WriteMessage($"\nDetected block type: \"{frontBlockType}\"");
                    }
                    else if (!string.Equals(frontBlockType, name, StringComparison.OrdinalIgnoreCase))
                    {
                        ed.WriteMessage($"\n✗ Error: Selected blocks are of different types!");
                        ed.WriteMessage($"   - First block type: \"{frontBlockType}\"");
                        ed.WriteMessage($"   - This block type: \"{name}\"");
                        ed.WriteMessage($"\n  Please select Front blocks of only ONE type and try again.");
                        return;
                    }

                    var data = new SourceBlockData
                    {
                        SourceId = id,
                        Attributes = CollectAttributeValues(tr, br),
                        DynamicProps = CollectDynamicPropertyValues(br)
                    };

                    frontBlocksData.Add(data);
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n✓ All {blockIds.Count} selected blocks are of type: \"{frontBlockType}\"");

            // --- Step 3: select the PI (Production Instruction) block ---
            ed.WriteMessage("\n--- STEP 2: Select the PI Block ---");
            ed.WriteMessage("\nPlease select the PI (Production Instruction) block to use as a template.");
            ed.WriteMessage("\nThis is the block where you want attributes/properties to be copied from.");

            var peoPi = new PromptEntityOptions("\nSelect PI block: ");
            peoPi.SetRejectMessage("✗ Only block references are allowed. Please select a block.");
            peoPi.AddAllowedClass(typeof(BlockReference), exactMatch: false);

            var perPi = ed.GetEntity(peoPi);
            if (perPi.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n✗ Cancelled by user.");
                return;
            }

            ed.WriteMessage($"\n✓ PI block selected.");

            // --- Step 4: validate that PI block's attributes/props are contained in Front block's attributes/props ---
            ed.WriteMessage("\n--- Validating PI Block Compatibility ---");

            var piAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var piDynamicProps = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var piBlock = (BlockReference)tr.GetObject(perPi.ObjectId, OpenMode.ForRead);
                piAttributes = CollectAttributeValues(tr, piBlock);
                piDynamicProps = CollectDynamicPropertyValues(piBlock);
                tr.Commit();
            }

            // Check that PI block attributes/props are contained in Front block's attributes/props
            var sampleFrontBlock = frontBlocksData[0];
            var frontAttrNames = new HashSet<string>(sampleFrontBlock.Attributes.Keys, StringComparer.OrdinalIgnoreCase);
            var frontPropNames = new HashSet<string>(sampleFrontBlock.DynamicProps.Keys, StringComparer.OrdinalIgnoreCase);

            var piAttrNames = new HashSet<string>(piAttributes.Keys, StringComparer.OrdinalIgnoreCase);
            var piPropNames = new HashSet<string>(piDynamicProps.Keys, StringComparer.OrdinalIgnoreCase);

            var piAttrNamesForCompare = new HashSet<string>(piAttrNames, StringComparer.OrdinalIgnoreCase);
            piAttrNamesForCompare.Remove(QuantityTag);

            // Check if PI block attributes/props are a subset of Front block attributes/props
            var missingAttrs = piAttrNamesForCompare.Except(frontAttrNames, StringComparer.OrdinalIgnoreCase).ToList();
            var missingProps = piPropNames.Except(frontPropNames, StringComparer.OrdinalIgnoreCase).ToList();

            if (missingAttrs.Count > 0 || missingProps.Count > 0)
            {
                ed.WriteMessage($"\n✗ Error: PI block has attributes/properties not found in Front block!");
                if (missingAttrs.Count > 0) 
                    ed.WriteMessage($"\n  Attributes in PI but not in Front block: {string.Join(", ", missingAttrs)}");
                if (missingProps.Count > 0) 
                    ed.WriteMessage($"\n  Dynamic properties in PI but not in Front block: {string.Join(", ", missingProps)}");
                ed.WriteMessage($"\nPlease select a compatible PI block and try again.");
                return;
            }

            ed.WriteMessage($"\n✓ PI block is compatible with Front blocks.");

            // --- Step 5: select the NET block ---
            ed.WriteMessage("\n--- STEP 3: Select the NET Block ---");
            ed.WriteMessage("\nPlease select the NET block where the output will be placed.");
            ed.WriteMessage("\nThis is the grid/container block that will hold the generated blocks.");

            var peoNet = new PromptEntityOptions("\nSelect NET block: ");
            peoNet.SetRejectMessage("✗ Only block references are allowed. Please select a block.");
            peoNet.AddAllowedClass(typeof(BlockReference), exactMatch: false);

            var perNet = ed.GetEntity(peoNet);
            if (perNet.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n✗ Cancelled by user.");
                return;
            }

            ed.WriteMessage($"\n✓ NET block selected.");

            Point3d netOrigin;
            double cellWidth;
            double cellHeight;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var netBr = (BlockReference)tr.GetObject(perNet.ObjectId, OpenMode.ForRead);
                // NET origin = top-left corner of the grid (left edge X, top edge Y)
                var extents = netBr.GeometricExtents;
                netOrigin = new Point3d(extents.MinPoint.X, extents.MaxPoint.Y, extents.MinPoint.Z);
                cellWidth = (extents.MaxPoint.X - extents.MinPoint.X) / ColumnsPerRow;
                cellHeight = cellWidth / 2.0;
                tr.Commit();
            }

            ed.WriteMessage($"\n✓ NET block dimensions calculated.");
            ed.WriteMessage($"\n  Grid layout: {ColumnsPerRow} columns");
            ed.WriteMessage($"  Cell size: {cellWidth:0.##} x {cellHeight:0.##}");

            // --- Step 6: get the PI block template reference ---
            ObjectId piTemplateId = perPi.ObjectId;

            // --- Step 7: for each Front block, deduplicate by fingerprint and create blocks from PI template ---
            ed.WriteMessage("\n--- STEP 4: Processing and Generating Blocks ---");
            ed.WriteMessage($"\nProcessing {frontBlocksData.Count} Front block(s)...");

            int cellIndex = 0;

            // KEY = NAME attribute value, VALUE = list of (created block ObjectId, source ObjectIds in that fingerprint group)
            // Used after creation to detect NAME conflicts and draw revclouds.
            var nameToCreated = new Dictionary<string, List<(ObjectId CreatedBlockId, List<ObjectId> SourceIds)>>(StringComparer.OrdinalIgnoreCase);

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ms = (BlockTableRecord)tr.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

                var piBlock = (BlockReference)tr.GetObject(piTemplateId, OpenMode.ForRead);

                // Group Front blocks by fingerprint (identical attr+prop values → same generated block with higher Quantity)
                var fingerGroups = new Dictionary<string, List<SourceBlockData>>(StringComparer.Ordinal);
                foreach (var src in frontBlocksData)
                {
                    string fp = src.GetFingerprint();
                    if (!fingerGroups.ContainsKey(fp))
                        fingerGroups[fp] = new List<SourceBlockData>();
                    fingerGroups[fp].Add(src);
                }

                ed.WriteMessage($"\nFound {fingerGroups.Count} unique block configuration(s) based on attributes.");

                foreach (var group in fingerGroups.Values)
                {
                    int quantity = group.Count;
                    var representative = group[0];

                    // Calculate grid cell position (center of cell)
                    int col = cellIndex % ColumnsPerRow;
                    int row = cellIndex / ColumnsPerRow;
                    double cellX = netOrigin.X + (col * cellWidth) + (cellWidth / 2.0);
                    double cellY = netOrigin.Y - (row * cellHeight) - (cellHeight / 2.0);
                    var cellPos = new Point3d(cellX, cellY, netOrigin.Z);
                    cellIndex++;

                    // Create new block reference using PI block as template
                    // Use DynamicBlockTableRecord so dynamic properties are available on the new instance
                    var btrId = piBlock.IsDynamicBlock
                        ? piBlock.DynamicBlockTableRecord
                        : piBlock.BlockTableRecord;

                    var newBr = new BlockReference(cellPos, btrId);
                    newBr.ScaleFactors = piBlock.ScaleFactors;
                    newBr.Rotation = piBlock.Rotation;
                    newBr.Normal = piBlock.Normal;

                    ms.AppendEntity(newBr);
                    tr.AddNewlyCreatedDBObject(newBr, true);

                    // Add attributes from the BTR definition
                    var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                    foreach (ObjectId entId in btr)
                    {
                        var dbObj = tr.GetObject(entId, OpenMode.ForRead);
                        if (dbObj is AttributeDefinition attDef && !attDef.Constant
                            && !attDef.Tag.StartsWith(IgnoredAttributePrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            var attRef = new AttributeReference();
                            attRef.SetAttributeFromBlock(attDef, newBr.BlockTransform);

                            if (string.Equals(attDef.Tag, QuantityTag, StringComparison.OrdinalIgnoreCase))
                            {
                                attRef.TextString = quantity.ToString();
                            }
                            else if (representative.Attributes.TryGetValue(attDef.Tag, out string val))
                            {
                                attRef.TextString = val;
                            }
                            else if (piAttributes.TryGetValue(attDef.Tag, out string piVal))
                            {
                                attRef.TextString = piVal;
                            }

                            newBr.AttributeCollection.AppendAttribute(attRef);
                            tr.AddNewlyCreatedDBObject(attRef, true);
                        }
                    }

                    // Set dynamic properties
                    if (newBr.IsDynamicBlock)
                    {
                        try
                        {
                            var dynProps = newBr.DynamicBlockReferencePropertyCollection;
                            if (dynProps != null)
                            {
                                foreach (DynamicBlockReferenceProperty p in dynProps)
                                {
                                    if (p.ReadOnly) continue;
                                    if (string.Equals(p.PropertyName, "Origin", StringComparison.OrdinalIgnoreCase)) continue;
                                    if (representative.DynamicProps.TryGetValue(p.PropertyName, out object srcVal))
                                    {
                                        try { p.Value = srcVal; }
                                        catch { }
                                    }
                                    else if (piDynamicProps.TryGetValue(p.PropertyName, out object piVal))
                                    {
                                        try { p.Value = piVal; }
                                        catch { }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    ed.WriteMessage($"\n  Created block (Qty={quantity}) at [{row},{col}]");

                    // Track NAME value → created block for conflict detection
                    if (representative.Attributes.TryGetValue(NameTag, out string nameVal) && !string.IsNullOrWhiteSpace(nameVal))
                    {
                        if (!nameToCreated.ContainsKey(nameVal))
                            nameToCreated[nameVal] = new List<(ObjectId, List<ObjectId>)>();
                        nameToCreated[nameVal].Add((newBr.ObjectId, group.Select(s => s.SourceId).ToList()));
                    }
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n✓ {cellIndex} block(s) generated and placed in the NET grid.");

            // --- Step 8: draw red revclouds around conflicting blocks (same NAME, different values) ---
            var conflictingNames = nameToCreated.Where(kv => kv.Value.Count > 1).ToList();
            if (conflictingNames.Count > 0)
            {
                ed.WriteMessage($"\n--- NAME Conflict Detection ---");
                ed.WriteMessage($"\n⚠ WARNING: Found {conflictingNames.Count} NAME conflict(s):");

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ms = (BlockTableRecord)tr.GetObject(
                        SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

                    foreach (var kv in conflictingNames)
                    {
                        string nameVal = kv.Key;
                        var entries = kv.Value;

                        ed.WriteMessage($"\n  • \"{nameVal}\" — {entries.Count} different configurations share this name.");
                        ed.WriteMessage($"    Drawing red revclouds around conflicting blocks...");

                        // Collect all block IDs that need a revcloud: the created blocks + their source blocks
                        var allConflictIds = new List<ObjectId>();
                        foreach (var (createdId, srcIds) in entries)
                        {
                            allConflictIds.Add(createdId);
                            allConflictIds.AddRange(srcIds);
                        }

                        foreach (var blockId in allConflictIds)
                        {
                            if (!blockId.IsValid) continue;
                            var br = tr.GetObject(blockId, OpenMode.ForRead) as BlockReference;
                            if (br == null) continue;

                            Extents3d ext;
                            try { ext = br.GeometricExtents; }
                            catch { continue; }

                            DrawRevcloud(tr, ms, ext, db);
                        }
                    }

                    tr.Commit();
                }

                ed.WriteMessage($"\n✓ Revclouds drawn for {conflictingNames.Count} conflicting NAME(s).");
            }

            ed.WriteMessage("\n========== NHB2BFlex Command Completed Successfully ==========\n");
        }

        #region Data Classes

        private class SourceBlockData
        {
            public ObjectId SourceId { get; set; }
            public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, object> DynamicProps { get; set; } = new(StringComparer.OrdinalIgnoreCase);

            public string GetFingerprint()
            {
                var parts = new List<string>();

                foreach (var kv in Attributes.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    parts.Add($"A:{kv.Key}={kv.Value}");

                foreach (var kv in DynamicProps.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    parts.Add($"P:{kv.Key}={ValueToString(kv.Value)}");

                return string.Join("|", parts);
            }
        }

        #endregion

        #region Helpers

        private static string GetBlockEffectiveName(Transaction tr, BlockReference br)
        {
            try
            {
                return ((BlockTableRecord)br.DynamicBlockTableRecord.GetObject(OpenMode.ForRead)).Name;
            }
            catch
            {
                // ignore
            }

            var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
            return btr.Name;
        }

        private static Dictionary<string, string> CollectAttributeValues(Transaction tr, BlockReference br)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (br.AttributeCollection == null || br.AttributeCollection.Count == 0)
                return dict;

            foreach (ObjectId attId in br.AttributeCollection)
            {
                if (!attId.IsValid) continue;
                var obj = tr.GetObject(attId, OpenMode.ForRead, false);
                if (obj is AttributeReference ar && !string.IsNullOrWhiteSpace(ar.Tag) && !IsIgnoredName(ar.Tag))
                    dict[ar.Tag] = ar.TextString ?? "";
            }

            return dict;
        }

        private static Dictionary<string, object> CollectDynamicPropertyValues(BlockReference br)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (!br.IsDynamicBlock)
                return dict;

            DynamicBlockReferencePropertyCollection props;
            try
            {
                props = br.DynamicBlockReferencePropertyCollection;
            }
            catch
            {
                return dict;
            }

            if (props == null || props.Count == 0)
                return dict;

            foreach (DynamicBlockReferenceProperty p in props)
            {
                if (p.ReadOnly) continue;
                if (string.Equals(p.PropertyName, "Origin", StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(p.PropertyName) && !IsIgnoredName(p.PropertyName))
                {
                    try { dict[p.PropertyName] = p.Value; }
                    catch { }
                }
            }

            return dict;
        }

        private static HashSet<string> CollectAttributeNames(Transaction tr, BlockReference br)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (br.AttributeCollection == null || br.AttributeCollection.Count == 0)
                return names;

            foreach (ObjectId attId in br.AttributeCollection)
            {
                if (!attId.IsValid) continue;
                var obj = tr.GetObject(attId, OpenMode.ForRead, false);
                if (obj is AttributeReference ar && !string.IsNullOrWhiteSpace(ar.Tag) && !IsIgnoredName(ar.Tag))
                    names.Add(ar.Tag);
            }

            return names;
        }

        private static HashSet<string> CollectDynamicPropertyNames(BlockReference br)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!br.IsDynamicBlock)
                return names;

            DynamicBlockReferencePropertyCollection props;
            try
            {
                props = br.DynamicBlockReferencePropertyCollection;
            }
            catch
            {
                return names;
            }

            if (props == null || props.Count == 0)
                return names;

            foreach (DynamicBlockReferenceProperty p in props)
            {
                if (p.ReadOnly) continue;
                if (string.Equals(p.PropertyName, "Origin", StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(p.PropertyName) && !IsIgnoredName(p.PropertyName))
                    names.Add(p.PropertyName);
            }

            return names;
        }

        private static bool IsIgnoredName(string name)
            => name.StartsWith(IgnoredAttributePrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Draws a red revcloud (closed polyline with arc bulges) around the given extents,
        /// with a small padding. Arc chord length is scaled to the extents size.
        /// </summary>
        private static void DrawRevcloud(Transaction tr, BlockTableRecord ms, Extents3d ext, Database db)
        {
            const double paddingFactor = 0.05; // 5% padding on each side
            const double minArcChord = 50.0;

            double w = ext.MaxPoint.X - ext.MinPoint.X;
            double h = ext.MaxPoint.Y - ext.MinPoint.Y;

            double pad = Math.Max(Math.Max(w, h) * paddingFactor, minArcChord * 0.5);
            double x0 = ext.MinPoint.X - pad;
            double y0 = ext.MinPoint.Y - pad;
            double x1 = ext.MaxPoint.X + pad;
            double y1 = ext.MaxPoint.Y + pad;

            // Arc chord length: roughly 1/8 of the perimeter, clamped to a sensible range
            double perimeter = 2.0 * ((x1 - x0) + (y1 - y0));
            double chord = Math.Max(minArcChord, Math.Min(perimeter / 12.0, Math.Max(w, h) * 0.3));

            // Bulge for a semicircle-like arc bump (bulge = tan(theta/4), theta = arc angle)
            // For a convex outward bump: use negative bulge (arc bows outward relative to polyline direction)
            double bulge = -Math.Tan(Math.PI / 8.0); // 45° arc per chord segment, bumps outward

            var pts = new List<(double X, double Y)>();

            // Bottom edge: left to right
            AddSegments(pts, x0, y0, x1, y0, chord);
            // Right edge: bottom to top
            AddSegments(pts, x1, y0, x1, y1, chord);
            // Top edge: right to left
            AddSegments(pts, x1, y1, x0, y1, chord);
            // Left edge: top to bottom
            AddSegments(pts, x0, y1, x0, y0, chord);

            if (pts.Count < 2) return;

            var pline = new Polyline();
            pline.Normal = Vector3d.ZAxis;
            pline.Elevation = ext.MinPoint.Z;
            pline.Closed = true;
            pline.ColorIndex = 1; // red

            for (int i = 0; i < pts.Count; i++)
                pline.AddVertexAt(i, new Point2d(pts[i].X, pts[i].Y), bulge, 0, 0);

            ms.AppendEntity(pline);
            tr.AddNewlyCreatedDBObject(pline, true);
        }

        /// <summary>
        /// Divides the segment from (x0,y0) to (x1,y1) into sub-segments of approximately
        /// <paramref name="chord"/> length and appends the start points (not the final endpoint).
        /// </summary>
        private static void AddSegments(List<(double X, double Y)> pts, double x0, double y0, double x1, double y1, double chord)
        {
            double dx = x1 - x0;
            double dy = y1 - y0;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return;

            int n = Math.Max(1, (int)Math.Round(len / chord));
            double stepX = dx / n;
            double stepY = dy / n;

            for (int i = 0; i < n; i++)
                pts.Add((x0 + i * stepX, y0 + i * stepY));
        }

        private static string ValueToString(object v)
        {
            if (v == null) return "null";
            if (v is double d) return d.ToString("0.########");
            if (v is string s) return s;
            return v.ToString() ?? "";
        }

        #endregion
    }
}