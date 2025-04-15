#if UNITY_2022_2_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEngine;
using UnityEngine.UIElements;

[assembly: InternalsVisibleTo("Unity.Addressables.Editor.Tests")]
namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    internal static class BuildReportUtility
    {
        public static readonly string MainToolbar = nameof(MainToolbar);
        public static readonly string MainToolbarCollapseLeftPaneButton = nameof(MainToolbarCollapseLeftPaneButton);
        public static readonly string MainToolbarCollapseRightPaneButton = nameof(MainToolbarCollapseRightPaneButton);
        public static readonly string MainToolbarAddReportButton = nameof(MainToolbarAddReportButton);
        public static readonly string MainToolbarCollapseLeftPaneButtonIcon = nameof(MainToolbarCollapseLeftPaneButtonIcon);
        public static readonly string MainToolbarCollapseRightPaneButtonIcon = nameof(MainToolbarCollapseRightPaneButtonIcon);
        public static readonly string MainToolbarAddReportButtonIcon = nameof(MainToolbarAddReportButtonIcon);
        public static readonly string SearchField = nameof(SearchField);

        public static readonly string ReportsListItemContainerRighthandElements = nameof(ReportsListItemContainerRighthandElements);
        public static readonly string ReportsListItemContainerLefthandElements = nameof(ReportsListItemContainerLefthandElements);

        public static readonly string ReportsList = nameof(ReportsList);
        public static readonly string ReportsListItemBuildStatus = nameof(ReportsListItemBuildStatus);
        public static readonly string ReportsListItemBuildPlatform = nameof(ReportsListItemBuildPlatform);
        public static readonly string ReportsListItemBuildTimestamp = nameof(ReportsListItemBuildTimestamp);
        public static readonly string ReportsListItemBuildDuration = nameof(ReportsListItemBuildDuration);


        public static readonly string MainPanel = nameof(MainPanel);

        public static readonly string LeftMiddlePaneSplitter = nameof(LeftMiddlePaneSplitter);
        public static readonly string MiddleRightPaneSplitter = nameof(MiddleRightPaneSplitter);

        public static readonly string ContentViewTypeDropdown = nameof(ContentViewTypeDropdown);

        public static readonly string ContentView = nameof(ContentView);

        // Bundle view
        public static readonly string BundlesContentViewColBundleName = nameof(BundlesContentViewColBundleName);
        public static readonly string BundlesContentViewColRefsTo = nameof(BundlesContentViewColRefsTo);
        public static readonly string BundlesContentViewColRefsBy = nameof(BundlesContentViewColRefsBy);
        public static readonly string BundlesContentViewColSizePlusRefs = nameof(BundlesContentViewColSizePlusRefs);
        public static readonly string BundlesContentViewColSizeUncompressed = nameof(BundlesContentViewColSizeUncompressed);
        public static readonly string BundlesContentViewBundleSize = nameof(BundlesContentViewBundleSize);


        // Asset view
        public static readonly string AssetsContentViewColAssetName = nameof(AssetsContentViewColAssetName);
        public static readonly string AssetsContentViewColSizePlusRefs = nameof(AssetsContentViewColSizePlusRefs);
        public static readonly string AssetsContentViewColSizeUncompressed = nameof(AssetsContentViewColSizeUncompressed);
        public static readonly string AssetsContentViewColBundleSize = nameof(AssetsContentViewColBundleSize);
        public static readonly string AssetsContentViewColRefsTo = nameof(AssetsContentViewColRefsTo);
        public static readonly string AssetsContentViewColRefsBy = nameof(AssetsContentViewColRefsBy);

        // Groups view
        public static readonly string GroupsContentViewColGroupName = nameof(GroupsContentViewColGroupName);
        public static readonly string GroupsContentViewColSizePlusRefs = nameof(GroupsContentViewColSizePlusRefs);
        public static readonly string GroupsContentViewColSizeUncompressed = nameof(GroupsContentViewColSizeUncompressed);
        public static readonly string GroupsContentViewColBundleSize = nameof(GroupsContentViewColBundleSize);
        public static readonly string GroupsContentViewColRefsTo = nameof(GroupsContentViewColRefsTo);
        public static readonly string GroupsContentViewColRefsBy = nameof(GroupsContentViewColRefsBy);

        // Duplicated Assets view
        public static readonly string DuplicatedAssetsContentViewColAssetName = nameof(DuplicatedAssetsContentViewColAssetName);
        public static readonly string DuplicatedAssetsContentViewColSize = nameof(DuplicatedAssetsContentViewColSize);
        public static readonly string DuplicatedAssetsContentViewSpaceSaved = nameof(DuplicatedAssetsContentViewSpaceSaved);
        public static readonly string DuplicatedAssetsContentViewDuplicationCount = nameof(DuplicatedAssetsContentViewDuplicationCount);

        // Cell stylesheets
        public static readonly string TreeViewImplicitAsset = nameof(TreeViewImplicitAsset);
        public static readonly string TreeViewDuplicatedAsset = nameof(TreeViewDuplicatedAsset);
        public static readonly string TreeViewElement = nameof(TreeViewElement);
        public static readonly string TreeViewAssetHeader = nameof(TreeViewAssetHeader);
        public static readonly string TreeViewHeader = nameof(TreeViewHeader);
        public static readonly string TreeViewIconElement = nameof(TreeViewIconElement);
        public static readonly string TreeViewItemIcon = nameof(TreeViewItemIcon);
        public static readonly string TreeViewItemNoIcon = "NoIcon";
        public static readonly string TreeViewItemName = nameof(TreeViewItemName);
        public static readonly string TreeViewItemFilePath = UxmlFilesPath + "TreeViewItem.uxml";
        public static readonly string TreeViewNavigableItemFilePath = UxmlFilesPath + "TreeViewNavigableItem.uxml";
        public static readonly string DrillableListViewItemPath = UxmlFilesPath + "DrillableListViewItem.uxml";

        public const string SummaryTabLabelElementNameFormat = "SummaryTabLabel_{0}";
        public const string SummaryTabSizeElementNameFormat = "SummaryTabSize_{0}";

        public const string SummaryTabUssPath = StyleSheetsPath + "SummaryTab.uss";
        public const string SummaryTabDarkUssPath = StyleSheetsPath + "SummaryTabDark.uss";
        public const string SummaryTabLightUssPath = StyleSheetsPath + "SummaryTabLight.uss";

        // Details panel
        public static readonly string DetailsSummaryPane = nameof(DetailsSummaryPane);
        public static readonly string DetailsContentsList = nameof(DetailsContentsList);

        public static readonly string BreadcrumbToolbar = nameof(BreadcrumbToolbar);
        public static readonly string BreadcrumbToolbarName = nameof(BreadcrumbToolbarName);
        public static readonly string BreadcrumbToolbarBackButton = nameof(BreadcrumbToolbarBackButton);
        public static readonly string BreadcrumbToolbarIcon = nameof(BreadcrumbToolbarIcon);
        public static readonly string DrillableListViewButton = nameof(DrillableListViewButton);
        public static readonly string DrillableListViewItemName = nameof(DrillableListViewItemName);
        public static readonly string DrillableListViewItemIcon = nameof(DrillableListViewItemIcon);

        // Ribbon
        public static readonly string TabsRibbon = nameof(TabsRibbon);
        public static readonly string SummaryTab = nameof(SummaryTab);
        public static readonly string ContentTab = nameof(ContentTab);
        public static readonly string PotentialIssuesTab = nameof(PotentialIssuesTab);
        public static readonly string PotentialIssuesDropdown = nameof(PotentialIssuesDropdown);
        public static readonly string ReferencedByTab = nameof(ReferencedByTab);
        public static readonly string ReferencesToTab = nameof(ReferencesToTab);

        public static readonly string DetailsViewDarkPath = StyleSheetsPath + "DetailsViewDark.uss";
        public static readonly string DetailsViewLightPath = StyleSheetsPath + "DetailsViewLight.uss";

        public const string UIToolKitAssetsPath = "Packages/com.boxcat.addressables/Editor/BuildReportVisualizer/UIToolKitAssets/";
        public const string UxmlFilesPath = UIToolKitAssetsPath + "UXML/";
        public const string StyleSheetsPath = UIToolKitAssetsPath + "StyleSheets/";

        public const string MainToolbarButtonsUssPath = StyleSheetsPath + "MainToolbarButtons.uss";
        public const string SideBarDark = "Packages/com.boxcat.addressables/Editor/BuildReportVisualizer/BuildReport Resources/Icons/Button_LeftPanel_DarkTheme@2x.png";
        public const string SideBarLight = "Packages/com.boxcat.addressables/Editor/BuildReportVisualizer/BuildReport Resources/Icons/Button_LeftPanel_LightTheme@2x.png";


        internal static string GetAssetBundleIconPath()
        {
            return EditorGUIUtility.isProSkin ? AddressableIconNames.AssetBundleIconDark : AddressableIconNames.AssetBundleIconLight;
        }

        internal static string GetForwardIconPath()
        {
            return EditorGUIUtility.isProSkin ? AddressableIconNames.ForwardIconDark : AddressableIconNames.ForwardIconLight;
        }

        internal static string GetHelpIconPath()
        {
            return AddressableIconNames.HelpIcon;
        }

        internal static string GetBackIconPath()
        {
            return EditorGUIUtility.isProSkin ? AddressableIconNames.BackIconDark : AddressableIconNames.BackIconLight;
        }

        internal static string GetDetailsViewStylesheetPath()
        {
            return EditorGUIUtility.isProSkin ? BuildReportUtility.DetailsViewDarkPath : BuildReportUtility.DetailsViewLightPath;
        }

        public static Texture GetIcon(string path)
        {
            if (File.Exists(path))
                return AssetDatabase.GetCachedIcon(path);
            else
                return EditorGUIUtility.FindTexture(path);
        }

        public static void SwitchClasses(this VisualElement element, string classToAdd, string classToRemove)
        {
            if (!element.ClassListContains(classToAdd))
                element.AddToClassList(classToAdd);
            element.RemoveFromClassList(classToRemove);
        }

        public static void SwitchVisibility(VisualElement first, VisualElement second, bool showFirst = true)
        {
            SetVisibility(first, showFirst);
            SetVisibility(second, !showFirst);
        }

        public static void SetVisibility(VisualElement element, bool visible)
        {
            SetElementDisplay(element, visible);
        }

        public static void SetElementDisplay(VisualElement element, bool value)
        {
            if (element == null)
                return;

            element.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            element.style.visibility = value ? Visibility.Visible : Visibility.Hidden;
        }

        public static VisualElement Clone(this VisualTreeAsset tree, VisualElement target = null, string styleSheetPath = null, Dictionary<string, VisualElement> slots = null)
        {
            var ret = tree.CloneTree();
            if (!string.IsNullOrEmpty(styleSheetPath))
                ret.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath));
            if (target != null)
                target.Add(ret);
            ret.style.flexGrow = 1f;
            return ret;
        }

        public static string GetIconClassName(BuildTarget target)
        {
            string iconClassName;
            switch (target)
            {
                case BuildTarget.Android:
                    iconClassName = BuildTarget.Android.ToString();
                    break;
                case BuildTarget.StandaloneOSX:
                    iconClassName = BuildTarget.StandaloneOSX.ToString();
                    break;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    iconClassName = BuildTarget.StandaloneWindows.ToString();
                    break;
                case BuildTarget.iOS:
                    iconClassName = BuildTarget.iOS.ToString();
                    break;
                case BuildTarget.StandaloneLinux64:
                    iconClassName = BuildTarget.StandaloneLinux64.ToString();
                    break;
                case BuildTarget.WebGL:
                    iconClassName = BuildTarget.WebGL.ToString();
                    break;
                case BuildTarget.WSAPlayer:
                    iconClassName = BuildTarget.WSAPlayer.ToString();
                    break;
                case BuildTarget.PS4:
                case BuildTarget.PS5:
                    iconClassName = BuildTarget.PS4.ToString();
                    break;
                case BuildTarget.XboxOne:
                case BuildTarget.GameCoreXboxOne:
                case BuildTarget.GameCoreXboxSeries:
                    iconClassName = BuildTarget.XboxOne.ToString();
                    break;
                case BuildTarget.tvOS:
                    iconClassName = BuildTarget.tvOS.ToString();
                    break;
                case BuildTarget.Switch:
                    iconClassName = BuildTarget.Switch.ToString();
                    break;
#if !UNITY_2022_2_OR_NEWER
                case BuildTarget.Lumin:
                    iconClassName = BuildTarget.Lumin.ToString();
                    break;
# endif
                default:
                    iconClassName = "NoIcon";
                    break;
            }
            return iconClassName;
        }
        public static string GetDenominatedBytesString(ulong bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";

            int dec = 0;
            ulong kbytes = bytes / 1024;
            if (kbytes < 1024)
            {
                dec = Mathf.FloorToInt(((bytes % 1024) / 1024f) * 100);
                return $"{kbytes}.{Mathf.FloorToInt(dec)} KB";
            }
            ulong mbytes = kbytes / 1024;
            if (mbytes < 1024)
            {
                dec = Mathf.FloorToInt(((kbytes % 1024) / 1024f) * 100);
                return $"{mbytes}.{Mathf.FloorToInt(dec)} MB";
            }

            ulong gbytes = mbytes / 1024;
            dec = Mathf.FloorToInt(((mbytes % 1024) / 1024f) * 100);
            return $"{gbytes}.{Mathf.FloorToInt(dec)} GB";
        }

        internal static string GetDeliminatedList(char delimChar, List<string> lst)
        {
            string itemsStr = string.Empty;
            for (int i = 0; i < lst.Count; i++)
            {
                if (i > 0)
                    itemsStr += $"{delimChar} ";
                itemsStr += lst[i];
            }
            return itemsStr;
        }

        internal static class TimeAgo
        {
            private const int k_Second = 1;
            private const int k_Minute = 60 * k_Second;
            private const int k_Hour = 60 * k_Minute;
            private const int k_Day = 24 * k_Hour;
            private const int k_Month = 30 * k_Day;
            public static string GetString(DateTime dateTime)
            {
                var ts = new TimeSpan(DateTime.UtcNow.Ticks - dateTime.ToUniversalTime().Ticks);
                double delta = Math.Abs(ts.TotalSeconds);
                if (delta < 1 * k_Minute)
                    return "Just now";
                if (delta < 2 * k_Minute)
                    return "a minute ago";
                if (delta < 45 * k_Minute)
                    return ts.Minutes + " minutes ago";
                if (delta < 90 * k_Minute)
                    return "an hour ago";
                if (delta < 24 * k_Hour)
                    return ts.Hours + " hours ago";
                if (delta < 48 * k_Hour)
                    return "yesterday";
                if (delta < 30 * k_Day)
                    return ts.Days + " days ago";
                if (delta < 12 * k_Month)
                {
                    int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
                    return months <= 1 ? "a month ago" : months + " months ago";
                }
                int years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
                return years <= 1 ? "one year ago" : years + " years ago";
            }
        }

        internal static SortedDictionary<AssetId, BuildLayout.ExplicitAsset> GetReferencingAssets(BuildLayout.ExplicitAsset asset)
        {
            var dependenciesOfAsset = new SortedDictionary<AssetId, BuildLayout.ExplicitAsset>();
            if (asset.Bundle == null)
                return dependenciesOfAsset;

            var assetsOfDependentBundles = asset.Bundle.DependentBundles.SelectMany(b => b.DependentBundles).SelectMany(f => f.Files).SelectMany(a => a.Assets);
            foreach (var assetOfDependentBundle in assetsOfDependentBundles)
            {
                if (assetOfDependentBundle.ExternallyReferencedAssets.Find(x => x.AddressableName == asset.AddressableName) != null)
                    dependenciesOfAsset.TryAdd(assetOfDependentBundle.Guid, assetOfDependentBundle);
            }
            return dependenciesOfAsset;
        }

        public static Button CreateButton(string text, Action action)
        {
            Button button = new Button(action);
            button.text = text;
            return button;
        }

        internal static Hash128 ComputeDataHash(string parentLabelName, string subLabelName = "")
        {
            Hash128 hash = Hash128.Compute(parentLabelName);
            hash.Append(subLabelName);
            return hash;
        }

        internal static VisualElement GetSeparatingLine()
        {
            VisualElement line = new VisualElement();
            line.style.width = new Length(100f, LengthUnit.Percent);
            line.style.height = new Length(1f, LengthUnit.Pixel);
            line.style.backgroundColor = new StyleColor(new Color(63f, 63f, 63f, 0.3f));
            return line;
        }
    }
}
#endif
