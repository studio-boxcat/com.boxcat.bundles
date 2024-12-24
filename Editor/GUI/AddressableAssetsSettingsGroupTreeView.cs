using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.GUI
{
    using Object = UnityEngine.Object;

    internal class AddressableAssetEntryTreeView : TreeView
    {
        AddressableAssetsSettingsGroupEditor m_Editor;
        internal string customSearchString = string.Empty;
        string m_FirstSelectedGroup;
        private readonly Dictionary<AssetEntryTreeViewItem, bool> m_SearchedEntries = new();
        private bool m_ForceSelectionClear = false;

        enum ColumnId
        {
            Id,
            Type,
            Path,
        }

        public AddressableAssetEntryTreeView(TreeViewState state, MultiColumnHeaderState mchs, AddressableAssetsSettingsGroupEditor ed) : base(state, new MultiColumnHeader(mchs))
        {
            showBorder = true;
            m_Editor = ed;
            columnIndexForTreeFoldouts = 0;
            multiColumnHeader.canSort = false;
            BuiltinSceneCache.sceneListChanged += OnScenesChanged;
        }

        internal TreeViewItem Root => rootItem;

        void OnScenesChanged()
        {
            if (m_Editor.Catalog == null)
                return;
            Reload();
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (selectedIds.Count == 1)
            {
                var item = FindItemInVisibleRows(selectedIds[0]);
                if (item != null && item.group != null)
                {
                    m_FirstSelectedGroup = item.group.name;
                }
            }

            base.SelectionChanged(selectedIds);

            var selectedObjects = new Object[selectedIds.Count];
            for (int i = 0; i < selectedIds.Count; i++)
            {
                var item = FindItemInVisibleRows(selectedIds[i]);
                if (item != null)
                {
                    if (item.group != null)
                        selectedObjects[i] = item.group;
                    else if (item.entry != null)
                        selectedObjects[i] = item.entry.MainAsset;
                }
            }

            // Make last selected group the first object in the array
            if (!string.IsNullOrEmpty(m_FirstSelectedGroup) && selectedObjects.Length > 1)
            {
                for (int i = 0; i < selectedObjects.Length - 1; ++i)
                {
                    if (selectedObjects[i] != null && selectedObjects[i].name == m_FirstSelectedGroup)
                    {
                        (selectedObjects[i], selectedObjects[selectedIds.Count - 1])
                            = (selectedObjects[selectedIds.Count - 1], selectedObjects[i]);
                    }
                }
            }

            Selection.objects = selectedObjects; // change selection
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(-1, -1);
            foreach (var group in m_Editor.Catalog.groups)
                AddGroupChildrenBuild(group, root);
            return root;
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            if (!string.IsNullOrEmpty(searchString))
            {
                var rows = base.BuildRows(root);
                SortHierarchical(rows);
                return rows;
            }

            if (!string.IsNullOrEmpty(customSearchString))
            {
                SortChildren(root);
                return Search(base.BuildRows(root));
            }

            SortChildren(root);
            return base.BuildRows(root);
        }

        internal void Search(string search)
        {
            searchString = search;
        }

        protected IList<TreeViewItem> Search(IList<TreeViewItem> rows)
        {
            if (rows == null)
                return new List<TreeViewItem>();

            m_SearchedEntries.Clear();
            var items = new List<TreeViewItem>(rows.Count);
            foreach (var item in rows)
            {
                if (DoesItemMatchSearch(item, searchString))
                    items.Add(item);
            }

            return items;
        }

        void SortChildren(TreeViewItem root)
        {
            if (!root.hasChildren)
                return;

            foreach (var child in root.children)
            {
                if (child != null && IsExpanded(child.id))
                    SortHierarchical(child.children);
            }
        }

        void SortHierarchical(IList<TreeViewItem> children)
        {
            if (children == null)
                return;

            var kids = new List<AssetEntryTreeViewItem>();
            var copy = new List<TreeViewItem>(children);
            children.Clear();
            foreach (var c in copy)
            {
                if (c is AssetEntryTreeViewItem { entry: not null } child)
                    kids.Add(child);
                else
                    children.Add(c);
            }

            foreach (var kid in kids)
                children.Add(kid);

            foreach (var child in children)
            {
                if (child != null && IsExpanded(child.id))
                    SortHierarchical(child.children);
            }
        }

        protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
        {
            if (string.IsNullOrEmpty(search))
                return true;

            if (item is not AssetEntryTreeViewItem aeItem)
                return false;

            //check if item matches.
            if (aeItem.displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (aeItem.entry == null)
                return false;
            if (aeItem.assetPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        void AddGroupChildrenBuild(AddressableAssetGroup group, TreeViewItem root)
        {
            Assert.IsNotNull(group);
            var groupItem = new AssetEntryTreeViewItem(group, 0);
            root.AddChild(groupItem);
            foreach (var entry in group.entries)
                groupItem.AddChild(new AssetEntryTreeViewItem(entry, 1));
        }

        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);

            //TODO - this occasionally causes a "hot control" issue.
            if (m_ForceSelectionClear ||
                (Event.current.type == EventType.MouseDown &&
                 Event.current.button == 0 &&
                 rect.Contains(Event.current.mousePosition)))
            {
                SetSelection(Array.Empty<int>(), TreeViewSelectionOptions.FireSelectionChanged);
                if (m_ForceSelectionClear)
                    m_ForceSelectionClear = false;
            }
        }

        protected override void BeforeRowsGUI()
        {
            base.BeforeRowsGUI();

            if (Event.current.type is not EventType.Repaint) return;

            var rows = GetRows();
            if (rows.Count is 0) return;

            GetFirstAndLastVisibleRows(out var first, out var last);
            for (var rowId = first; rowId <= last; rowId++)
            {
                if (rows[rowId] is AssetEntryTreeViewItem { entry: not null })
                    DefaultStyles.backgroundEven.Draw(GetRowRect(rowId), false, false, false, false);
            }
        }

        GUIStyle m_LabelStyle;

        protected override void RowGUI(RowGUIArgs args)
        {
            m_LabelStyle ??= new GUIStyle("PR Label");

            if (args.item is not AssetEntryTreeViewItem item
                || item.group == null && item.entry == null)
            {
                using (new EditorGUI.DisabledScope(true))
                    base.RowGUI(args);
            }
            else
            {
                if (item.group != null)
                {
                    if (item.isRenaming && !args.isRenaming)
                        item.isRenaming = false;
                }

                for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                    CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);
            }
        }

        void CellGUI(Rect cellRect, AssetEntryTreeViewItem item, int column, ref RowGUIArgs args)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch ((ColumnId) column)
            {
                case ColumnId.Id:
                    args.rowRect = cellRect;
                    base.RowGUI(args);
                    break;
                case ColumnId.Path:
                    if (item.entry != null && Event.current.type == EventType.Repaint)
                    {
                        var path = item.assetPath;
                        if (string.IsNullOrEmpty(path))
                            path = "Missing File";
                        m_LabelStyle.Draw(cellRect, path, false, false, args.selected, args.focused);
                    }
                    break;
                case ColumnId.Type:
                    if (item.assetIcon != null)
                        UnityEngine.GUI.DrawTexture(cellRect, item.assetIcon, ScaleMode.ScaleToFit, true);
                    break;
            }
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState()
        {
            return new MultiColumnHeaderState(GetColumns());
        }

        static MultiColumnHeaderState.Column[] GetColumns()
        {
            var retVal = new[]
            {
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
            };

            int counter = 0;

            retVal[counter].headerContent = new GUIContent("Group Name \\ Addressable Name", "Address used to load asset at runtime");
            retVal[counter].minWidth = 100;
            retVal[counter].width = 260;
            retVal[counter].maxWidth = 10000;
            retVal[counter].headerTextAlignment = TextAlignment.Left;
            retVal[counter].autoResize = true;
            counter++;

            retVal[counter].headerContent = new GUIContent(EditorGUIUtility.FindTexture("FilterByType"), "Asset type");
            retVal[counter].minWidth = 20;
            retVal[counter].width = 20;
            retVal[counter].maxWidth = 20;
            retVal[counter].headerTextAlignment = TextAlignment.Left;
            retVal[counter].autoResize = true;
            counter++;

            retVal[counter].headerContent = new GUIContent("Path", "Current Path of asset");
            retVal[counter].minWidth = 100;
            retVal[counter].width = 300;
            retVal[counter].maxWidth = 10000;
            retVal[counter].headerTextAlignment = TextAlignment.Left;
            retVal[counter].autoResize = true;

            return retVal;
        }

        protected string CheckForRename(TreeViewItem item, bool isActualRename)
        {
            string result = string.Empty;
            var assetItem = item as AssetEntryTreeViewItem;
            if (assetItem != null)
            {
                if (assetItem.group != null)
                    result = "Rename";
                else if (assetItem.entry != null)
                    result = "Change Address";
                if (isActualRename)
                    assetItem.isRenaming = !string.IsNullOrEmpty(result);
            }

            return result;
        }

        protected override bool CanRename(TreeViewItem item)
        {
            return !string.IsNullOrEmpty(CheckForRename(item, true));
        }

        AssetEntryTreeViewItem FindItemInVisibleRows(int id)
        {
            var rows = GetRows();
            foreach (var r in rows)
            {
                if (r.id == id)
                {
                    return r as AssetEntryTreeViewItem;
                }
            }

            return null;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            if (!args.acceptedRename)
                return;

            var item = FindItemInVisibleRows(args.itemID);
            if (item != null)
            {
                item.isRenaming = false;
            }

            if (args.originalName == args.newName)
                return;

            if (item != null)
            {
                Assert.IsNotNull(args.newName, "args.newName is null");
                if (item.entry != null)
                {
                    item.entry.SetAddress(args.newName);
                }
                else if (item.group != null)
                {
                    if (m_Editor.Catalog.IsNotUniqueGroupName(args.newName))
                    {
                        args.acceptedRename = false;
                        L.W("There is already a group named '" + args.newName + "'.  Cannot rename this group to match");
                    }
                    else
                    {
                        item.group.Name = args.newName;
                    }
                }

                Reload();
            }
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return true;
        }

        protected override void DoubleClickedItem(int id)
        {
            var item = FindItemInVisibleRows(id);
            if (item != null)
            {
                Object o = null;
                if (item.entry != null)
                    o = item.entry.MainAsset;
                else if (item.group != null)
                    o = item.group;

                if (o != null)
                {
                    EditorGUIUtility.PingObject(o);
                    Selection.activeObject = o;
                }
            }
        }

        bool m_ContextOnItem;

        protected override void ContextClicked()
        {
            if (m_ContextOnItem)
            {
                m_ContextOnItem = false;
                return;
            }

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Create New Group"), false, CreateNewGroup);
            menu.ShowAsContext();
        }

        protected override void ContextClickedItem(int id)
        {
            List<AssetEntryTreeViewItem> selectedNodes = new List<AssetEntryTreeViewItem>();
            foreach (var nodeId in GetSelection())
            {
                var item = FindItemInVisibleRows(nodeId); //TODO - this probably makes off-screen but selected items not get added to list.
                if (item != null)
                    selectedNodes.Add(item);
            }

            if (selectedNodes.Count == 0)
                return;

            m_ContextOnItem = true;

            bool isGroup = false;
            bool isEntry = false;
            foreach (var item in selectedNodes)
            {
                if (item.group != null)
                {
                    isGroup = true;
                }
                else if (item.entry != null)
                {
                    isEntry = true;
                }
            }

            if (isEntry && isGroup)
                return;

            var menu = new GenericMenu();
            if (isGroup)
            {
                var group = selectedNodes.First().group;
                if (!group.Default)
                    menu.AddItem(new GUIContent("Remove Group(s)"), false, RemoveGroup, selectedNodes);
                if (selectedNodes.Count is 1)
                    menu.AddItem(new GUIContent("Inspect Group Settings"), false, GoToGroupAsset, selectedNodes);
            }
            else if (isEntry)
            {
                menu.AddItem(new GUIContent("Remove Addressables"), false, RemoveEntry, selectedNodes);
            }

            if (selectedNodes.Count == 1)
            {
                var label = CheckForRename(selectedNodes.First(), false);
                if (!string.IsNullOrEmpty(label))
                    menu.AddItem(new GUIContent(label), false, RenameItem, selectedNodes);
            }

            menu.AddItem(new GUIContent("Create New Group"), false, CreateNewGroup);

            menu.ShowAsContext();
        }

        static void GoToGroupAsset(object context)
        {
            if (context is not List<AssetEntryTreeViewItem> selectedNodes || selectedNodes.Count == 0)
                return;
            var group = selectedNodes.First().group;
            if (group == null)
                return;
            EditorGUIUtility.PingObject(group);
            Selection.activeObject = group;
        }

        internal void CreateNewGroup()
        {
            m_Editor.Catalog.CreateGroup(false);
            Reload();
        }

        protected void RemoveGroup(object context)
        {
            RemoveGroupImpl(context);
        }

        internal void RemoveGroupImpl(object context, bool forceRemoval = false)
        {
            if (forceRemoval || EditorUtility.DisplayDialog("Delete selected groups?", "Are you sure you want to delete the selected groups?\n\nYou cannot undo this action.", "Yes", "No"))
            {
                List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
                if (selectedNodes == null || selectedNodes.Count < 1)
                    return;
                var groups = new List<AddressableAssetGroup>();
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var item in selectedNodes)
                    {
                        m_Editor.Catalog.RemoveGroupInternal(item == null ? null : item.group, true, false);
                        groups.Add(item.group);
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                m_Editor.Catalog.SetDirty(AddressableCatalog.ModificationEvent.GroupRemoved, groups, true, true);
            }
        }

        protected void RemoveEntry(object context)
        {
            var selectedNodes = context as List<AssetEntryTreeViewItem>;
            if (selectedNodes == null || selectedNodes.Count is 0)
                return;

            var entries = new List<AddressableAssetEntry>();
            var modifiedGroups = new HashSet<AddressableAssetGroup>();
            foreach (var item in selectedNodes)
            {
                if (item.entry != null)
                {
                    entries.Add(item.entry);
                    modifiedGroups.Add(item.entry.parentGroup);
                    m_Editor.Catalog.RemoveAssetEntry(item.entry.guid, false);
                }
            }

            foreach (var g in modifiedGroups)
                g.SetDirty(AddressableCatalog.ModificationEvent.EntryModified, entries, false, true);

            m_Editor.Catalog.SetDirty(AddressableCatalog.ModificationEvent.EntryRemoved, entries, true, false);
        }

        protected void RenameItem(object context)
        {
            RenameItemImpl(context);
        }

        internal void RenameItemImpl(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            if (selectedNodes != null && selectedNodes.Count >= 1)
            {
                var item = selectedNodes.First();
                if (CanRename(item))
                    BeginRename(item);
            }
        }

        protected override bool CanBeParent(TreeViewItem item)
        {
            var aeItem = item as AssetEntryTreeViewItem;
            if (aeItem != null && aeItem.group != null)
                return true;

            return false;
        }

        protected override void KeyEvent()
        {
            if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Delete && GetSelection().Count > 0)
            {
                List<AssetEntryTreeViewItem> selectedNodes = new List<AssetEntryTreeViewItem>();
                bool allGroups = true;
                bool allEntries = true;
                foreach (var nodeId in GetSelection())
                {
                    var item = FindItemInVisibleRows(nodeId);
                    if (item != null)
                    {
                        selectedNodes.Add(item);
                        if (item.entry == null)
                            allEntries = false;
                        else
                            allGroups = false;
                    }
                }

                if (allEntries)
                    RemoveEntry(selectedNodes);
                if (allGroups)
                    RemoveGroup(selectedNodes);
            }
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            foreach (var id in args.draggedItemIDs)
            {
                var item = FindItemInVisibleRows(id);
                if (item is { entry: not null }) // Only group can be dragged.
                    return false;
            }

            return true;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            DragAndDrop.PrepareStartDrag();

            var selectedNodes = new List<AssetEntryTreeViewItem>();
            foreach (var id in args.draggedItemIDs)
            {
                var item = FindItemInVisibleRows(id);
                if (item.entry != null || item.@group != null)
                    selectedNodes.Add(item);
            }

            DragAndDrop.paths = null;
            DragAndDrop.objectReferences = new Object[] { };
            DragAndDrop.SetGenericData("AssetEntryTreeViewItem", selectedNodes);
            DragAndDrop.visualMode = selectedNodes.Count > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            DragAndDrop.StartDrag("AssetBundleTree");
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            DragAndDropVisualMode visualMode = DragAndDropVisualMode.None;

            var target = args.parentItem as AssetEntryTreeViewItem;

            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
            {
                visualMode = HandleDragAndDropPaths(target, args);
            }
            else
            {
                visualMode = HandleDragAndDropItems(target, args);
            }

            return visualMode;
        }

        DragAndDropVisualMode HandleDragAndDropItems(AssetEntryTreeViewItem target, DragAndDropArgs args)
        {
            DragAndDropVisualMode visualMode = DragAndDropVisualMode.None;

            var draggedNodes = DragAndDrop.GetGenericData("AssetEntryTreeViewItem") as List<AssetEntryTreeViewItem>;
            if (draggedNodes != null && draggedNodes.Count > 0)
            {
                visualMode = DragAndDropVisualMode.Copy;
                AssetEntryTreeViewItem firstItem = draggedNodes.First();
                bool isDraggingGroup = firstItem.IsGroup;
                bool isDraggingNestedGroup = isDraggingGroup && firstItem.parent != rootItem;
                bool dropParentIsRoot = args.parentItem == rootItem || args.parentItem == null;

                if (isDraggingNestedGroup || isDraggingGroup && !dropParentIsRoot || !isDraggingGroup && dropParentIsRoot)
                    visualMode = DragAndDropVisualMode.Rejected;

                if (args.performDrop)
                {
                    if (args.parentItem == null || args.parentItem == rootItem && visualMode != DragAndDropVisualMode.Rejected)
                    {
                        // Need to insert groups in reverse order because all groups will be inserted at the same index
                        for (int i = draggedNodes.Count - 1; i >= 0; i--)
                        {
                            AssetEntryTreeViewItem node = draggedNodes[i];
                            AddressableAssetGroup group = node.@group;
                            int index = m_Editor.Catalog.groups.FindIndex(g => g == group);
                            if (index < args.insertAtIndex)
                                args.insertAtIndex--;

                            m_Editor.Catalog.groups.RemoveAt(index);

                            if (args.insertAtIndex < 0 || args.insertAtIndex > m_Editor.Catalog.groups.Count)
                                m_Editor.Catalog.groups.Insert(m_Editor.Catalog.groups.Count, group);
                            else
                                m_Editor.Catalog.groups.Insert(args.insertAtIndex, group);
                        }

                        Reload();
                    }
                    else
                    {
                        AddressableAssetGroup parent = null;
                        if (target.group != null)
                            parent = target.group;
                        else if (target.entry != null)
                            parent = target.entry.parentGroup;

                        if (parent != null)
                        {
                            var entries = new List<AddressableAssetEntry>();
                            foreach (AssetEntryTreeViewItem node in draggedNodes)
                            {
                                entries.Add(node.entry);
                            }

                            {
                                var modifiedGroups = new HashSet<AddressableAssetGroup>();
                                modifiedGroups.Add(parent);
                                foreach (AddressableAssetEntry entry in entries)
                                {
                                    modifiedGroups.Add(entry.parentGroup);
                                    AddressableCatalog.MoveEntry(entry, parent, false);
                                }

                                m_Editor.Catalog.SetDirty(AddressableCatalog.ModificationEvent.EntryMoved, entries, true, false);
                            }
                        }
                    }
                }
            }

            return visualMode;
        }

        DragAndDropVisualMode HandleDragAndDropPaths(AssetEntryTreeViewItem target, DragAndDropArgs args)
        {
            var containsGroup = false;
            foreach (var path in DragAndDrop.paths)
            {
                if (PathPointsToAssetGroup(path))
                {
                    containsGroup = true;
                    break;
                }
            }

            if (target == null && !containsGroup)
                return DragAndDropVisualMode.Rejected;

            foreach (var path in DragAndDrop.paths)
            {
                if (!AddressableAssetUtility.IsPathValidForEntry(path) && (!PathPointsToAssetGroup(path) && target != rootItem))
                    return DragAndDropVisualMode.Rejected;
            }

            if (args.performDrop is false)
                return DragAndDropVisualMode.Copy;

            if (!containsGroup)
            {
                AddressableAssetGroup parent = null;
                bool targetIsGroup = false;
                if (target.group != null)
                {
                    parent = target.group;
                    targetIsGroup = true;
                }
                else if (target.entry != null)
                    parent = target.entry.parentGroup;

                if (parent != null)
                {
                    var resourcePaths = new List<string>();
                    var nonResourceGuids = new List<AssetGUID>();
                    foreach (var p in DragAndDrop.paths)
                    {
                        if (AddressableAssetUtility.IsInResources(p))
                            resourcePaths.Add(p);
                        else
                            nonResourceGuids.Add((AssetGUID) AssetDatabase.AssetPathToGUID(p));
                    }

                    if (resourcePaths.Count == 0)
                    {
                        if (nonResourceGuids.Count > 0)
                        {
                            var entriesMoved = new List<AddressableAssetEntry>();
                            var entriesCreated = new List<AddressableAssetEntry>();
                            m_Editor.Catalog.CreateOrMoveEntries(nonResourceGuids, parent, entriesCreated, entriesMoved, false);

                            if (entriesMoved.Count > 0)
                                m_Editor.Catalog.SetDirty(AddressableCatalog.ModificationEvent.EntryMoved, entriesMoved, true);
                            if (entriesCreated.Count > 0)
                                m_Editor.Catalog.SetDirty(AddressableCatalog.ModificationEvent.EntryAdded, entriesCreated, true);
                        }

                        if (targetIsGroup)
                        {
                            SetExpanded(target.id, true);
                        }
                    }
                }
            }
            else
            {
                var modified = false;
                foreach (var p in DragAndDrop.paths)
                {
                    if (PathPointsToAssetGroup(p) is false) continue;
                    var loadedGroup = AssetDatabase.LoadAssetAtPath<AddressableAssetGroup>(p);
                    if (loadedGroup is null) continue;
                    if (m_Editor.Catalog.FindGroup(g => g == loadedGroup) == null)
                    {
                        m_Editor.Catalog.groups.Add(loadedGroup);
                        modified = true;
                    }
                }

                if (modified)
                    m_Editor.Catalog.SetDirty(AddressableCatalog.ModificationEvent.GroupAdded,
                        m_Editor.Catalog, true, true);
            }

            return DragAndDropVisualMode.Copy;
        }

        private static bool PathPointsToAssetGroup(string path)
        {
            return AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(AddressableAssetGroup);
        }
    }

    class AssetEntryTreeViewItem : TreeViewItem
    {
        public AddressableAssetEntry entry;
        public AddressableAssetGroup group;
        public string assetPath;
        public Texture2D assetIcon;
        public bool isRenaming;

        public AssetEntryTreeViewItem(AddressableAssetEntry e, int d)
            : base(e == null ? 0 : (e.address + e.guid).GetHashCode(), d, e == null ? "[Missing Reference]" : e.address)
        {
            entry = e;
            group = null;
            assetPath = e is not null ? e.ResolveAssetPath() : "";
            assetIcon = assetPath is not "" ? AssetDatabase.GetCachedIcon(assetPath) as Texture2D : null;
            isRenaming = false;
        }

        public AssetEntryTreeViewItem(AddressableAssetGroup g, int d) : base(g == null ? 0 : g.GetInstanceID(), d, g == null ? "[Missing Reference]" : g.Name)
        {
            entry = null;
            group = g;
            assetIcon = null;
            isRenaming = false;
        }

        public bool IsGroup => group != null && entry == null;

        public override string displayName
        {
            get
            {
                var baseName = base.displayName;

                // If currently renaming, return the base name
                if (isRenaming)
                    return baseName;

                if (group is not null)
                {
                    if (group == null)
                    {
                        L.E("Group is destroyed");
                        return "Missing (Group)";
                    }

                    if (group.Default)
                        return baseName + " (Default)";
                }
                else
                {
                    if (string.IsNullOrEmpty(entry.address))
                    {
                        Assert.IsTrue(string.IsNullOrEmpty(baseName));
                        var asset = entry.MainAsset;
                        var assetName = asset != null ? asset.name : "Missing";
                        return "NA (" + assetName + ")";
                    }
                }

                return baseName;
            }

            set => base.displayName = value;
        }
    }
}