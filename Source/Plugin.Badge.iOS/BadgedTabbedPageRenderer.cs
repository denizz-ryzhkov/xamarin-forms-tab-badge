using Plugin.Badge.Abstractions;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;
using Xamarin.Forms.Internals;
using System.Threading.Tasks;
using UIKit;
using System;
using System.Linq;
using CoreGraphics;

namespace Plugin.Badge.iOS
{
    public enum BadgeShape
    {
        None,
        Dot,
        Counter
    }

    [Preserve]
    public class BadgedTabbedPageRenderer : TabbedRenderer
    {
        private BadgeShape BadgeType { get; set; }
        private const int _baseSubviewTag = 2230;

        protected override void OnElementChanged(VisualElementChangedEventArgs e)
        {
            base.OnElementChanged(e);

            // make sure we cleanup old event registrations
            Cleanup(e.OldElement as TabbedPage);
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);

            // make sure we cleanup old event registrations
            Cleanup(Tabbed);

            for (var i = 0; i < TabBar.Items.Length; i++)
            {
                AddTabBadge(i);
            }

            Tabbed.ChildAdded += OnTabAdded;
            Tabbed.ChildRemoved += OnTabRemoved;
        }

        private void AddTabBadge(int tabIndex)
        {
            var element = Tabbed.GetChildPageWithBadge(tabIndex);
            element.PropertyChanged += OnTabbedPagePropertyChanged;

            InitBadgeType(element);

            if (TabBar.Items.Length > tabIndex)
            {
                var tabBarItem = TabBar.Items[tabIndex];

                if (BadgeType == BadgeShape.Counter)
                {
                    UpdateTabBadgeText(tabIndex, tabBarItem, element);
                    UpdateTabBadgeColor(tabIndex, tabBarItem, element);
                    UpdateTabBadgeTextAttributes(tabBarItem, element);
                }
                else if (BadgeType == BadgeShape.Dot)
                    UpdateDotBadge(tabIndex, element);
                else
                    UpdateDotBadge(tabIndex, element, true);

            }
        }

        private void UpdateTabBadgeText(int tabIndex, UITabBarItem tabBarItem, Element element)
        {
            var text = TabBadge.GetBadgeText(element);

            InitBadgeType(element);

            switch (BadgeType)
            {
                case BadgeShape.Counter:
                    UpdateDotBadge(tabIndex, element, true);
                    tabBarItem.BadgeValue = text;
                    break;
                case BadgeShape.Dot:
                    tabBarItem.BadgeValue = null;
                    UpdateDotBadge(tabIndex, element);
                    break;
                case BadgeShape.None:
                    tabBarItem.BadgeValue = null;
                    UpdateDotBadge(tabIndex, element, true);
                    break;
                default:
                    break;
            }
        }

        private void UpdateTabBadgeTextAttributes(UITabBarItem tabBarItem, Element element)
        {
            if (BadgeType == BadgeShape.Dot)
                return;

            if (!tabBarItem.RespondsToSelector(new ObjCRuntime.Selector("setBadgeTextAttributes:forState:")))
            {
                // method not available, ios < 10
                Console.WriteLine("Plugin.Badge: badge text attributes only available starting with iOS 10.0.");
                return;
            }

            var attrs = new UIStringAttributes();

            var textColor = TabBadge.GetBadgeTextColor(element);
            if (textColor != Color.Default)
            {
                attrs.ForegroundColor = textColor.ToUIColor();
            }

            var font = TabBadge.GetBadgeFont(element);
            if (font != Font.Default)
            {
                attrs.Font = font.ToUIFont();
            }

            tabBarItem.SetBadgeTextAttributes(attrs, UIControlState.Normal);
        }

        private void UpdateTabBadgeColor(int tabIndex, UITabBarItem tabBarItem, Element element)
        {
            InitBadgeType(element);

            if (BadgeType == BadgeShape.Dot)
            {
                UpdateDotBadge(tabIndex, element);
                return;
            }

            if (!tabBarItem.RespondsToSelector(new ObjCRuntime.Selector("setBadgeColor:")))
            {
                // method not available, ios < 10
                Console.WriteLine("Plugin.Badge: badge color only available starting with iOS 10.0.");
                return;
            }

            var tabColor = TabBadge.GetBadgeColor(element);
            if (tabColor != Color.Default)
            {
                tabBarItem.BadgeColor = tabColor.ToUIColor();
            }
        }

        private void UpdateDotBadge(int index, Element element, bool isRemove = false)
        {
            foreach (var subview in TabBar.Subviews)
            {
                if (subview is UIView uIView && uIView.Tag == index + _baseSubviewTag)
                    subview.RemoveFromSuperview();
            }

            var text = TabBadge.GetBadgeText(element);
            if (isRemove || string.IsNullOrEmpty(text))
                return;

            var tabBarItem = TabBar.Items[index];

            var dotRadius = TabBadge.GetBadgeRadius(element);
            var dotDiameter = dotRadius * 2;

            var topMargin = TabBadge.GetBadgeMargin(element).Top;
            var leftMargin = TabBadge.GetBadgeMargin(element).Left;
            var rightMargin = TabBadge.GetBadgeMargin(element).Right * -1;

            var tabBarItemCount = TabBar.Items.Count();

            var screenSize = UIScreen.MainScreen.Bounds;
            var halfItemWidth = (screenSize.Width) / (tabBarItemCount * 2);

            var xOffset = halfItemWidth * (index * 2 + 1);
            var imageHalfWidth = tabBarItem?.SelectedImage == null ? 13 : tabBarItem.SelectedImage.Size.Width / 2;
            var additionalOffsetX = leftMargin + rightMargin;

            var dot = new UIView(frame: new CGRect(x: xOffset + imageHalfWidth - 7 + additionalOffsetX, y: topMargin, width: dotDiameter, height: dotDiameter));

            dot.Tag = index + _baseSubviewTag;
            dot.BackgroundColor = TabBadge.GetBadgeColor(element).ToUIColor();
            dot.Layer.CornerRadius = dotRadius;
            TabBar.AddSubview(dot);
        }

        private void OnTabbedPagePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var page = sender as Page;
            if (page == null)
                return;

            if (e.PropertyName == Page.IconImageSourceProperty.PropertyName)
            {
                // #65 update badge properties if icon changed
                if (CheckValidTabIndex(page, out int tabIndex))
                {
                    UpdateTabBadgeText(tabIndex, TabBar.Items[tabIndex], page);
                    UpdateTabBadgeColor(tabIndex, TabBar.Items[tabIndex], page);
                    UpdateTabBadgeTextAttributes(TabBar.Items[tabIndex], page);
                }

                return;
            }

            if (e.PropertyName == TabBadge.BadgeTextProperty.PropertyName
                || e.PropertyName == TabBadge.BadgeMarginProperty.PropertyName
                || e.PropertyName == TabBadge.BadgeRadiusProperty.PropertyName)
            {
                if (CheckValidTabIndex(page, out int tabIndex))
                    UpdateTabBadgeText(tabIndex, TabBar.Items[tabIndex], page);
                return;
            }

            if (e.PropertyName == TabBadge.BadgeColorProperty.PropertyName)
            {
                if (CheckValidTabIndex(page, out int tabIndex))
                    UpdateTabBadgeColor(tabIndex, TabBar.Items[tabIndex], page);
                return;
            }

            if (e.PropertyName == TabBadge.BadgeTextColorProperty.PropertyName || e.PropertyName == TabBadge.BadgeFontProperty.PropertyName)
            {
                if (CheckValidTabIndex(page, out int tabIndex))
                    UpdateTabBadgeTextAttributes(TabBar.Items[tabIndex], page);
                return;
            }
        }

        protected bool CheckValidTabIndex(Page page, out int tabIndex)
        {
            tabIndex = Tabbed.Children.IndexOf(page);
            if (tabIndex == -1 && page.Parent != null)
                tabIndex = Tabbed.Children.IndexOf(page.Parent);
            return tabIndex >= 0 && tabIndex < TabBar.Items.Length;
        }

        private async void OnTabAdded(object sender, ElementEventArgs e)
        {
            //workaround for XF, tabbar is not updated at this point and we have no way of knowing for sure when it will be updated. so we have to wait ... 
            await Task.Delay(10);
            var page = e.Element as Page;
            if (page == null)
                return;

            var tabIndex = Tabbed.Children.IndexOf(page);
            AddTabBadge(tabIndex);
        }

        private void OnTabRemoved(object sender, ElementEventArgs e)
        {
            e.Element.PropertyChanged -= OnTabbedPagePropertyChanged;
        }

        protected override void Dispose(bool disposing)
        {
            Cleanup(Tabbed);

            base.Dispose(disposing);
        }

        private void Cleanup(TabbedPage tabbedPage)
        {
            if (tabbedPage == null)
            {
                return;
            }

            foreach (var tab in tabbedPage.Children.Select(c => c.GetPageWithBadge()))
            {
                tab.PropertyChanged -= OnTabbedPagePropertyChanged;
            }

            tabbedPage.ChildAdded -= OnTabAdded;
            tabbedPage.ChildRemoved -= OnTabRemoved;
        }

        private void InitBadgeType(Element element)
        {
            var text = TabBadge.GetBadgeText(element);

            if (string.IsNullOrEmpty(text))
                BadgeType = BadgeShape.None;
            else if (text == "0")
                BadgeType = BadgeShape.Dot;
            else
                BadgeType = BadgeShape.Counter;
        }
    }
}
