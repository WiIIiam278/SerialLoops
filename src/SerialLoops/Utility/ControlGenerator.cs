﻿using Eto.Drawing;
using Eto.Forms;
using HaruhiChokuretsuLib.Util;
using SerialLoops.Controls;
using SerialLoops.Lib.Items;
using System;

namespace SerialLoops.Utility
{
    public static class ControlGenerator
    {
        public static StackLayout GetControlWithLabel(string title, Control control)
        {
            return new StackLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Spacing = 10,
                Items =
                {
                    title,
                    control,
                },
            };
        }

        public static TableLayout GetControlWithLabelTable(string title, Control control)
        {
            return new TableLayout(new TableRow(new Label { Text = title }, control))
            {
                Spacing = new Size(10, 5)
            };
        }

        public static StackLayout GetControlWithSuffix(Control control, string suffix)
        {
            return new StackLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Spacing = 2,
                Items =
                {
                    control,
                    suffix,
                }
            };
        }
        
        public static StackLayout GetFileLink(ItemDescription description, EditorTabsPanel editorTabs, ILogger log)
        {
            ClearableLinkButton link = new() { Text = description.DisplayName };
            if (description.Name != "NONE")
            {
                link.ClickUnique += GetFileLinkClickHandler(description, editorTabs, log);
            }
            return GetControlWithIcon(link, description.Type.ToString(), log);
        }

        public static EventHandler<EventArgs> GetFileLinkClickHandler(ItemDescription description, EditorTabsPanel editorTabs, ILogger log)
        {
            return (s, e) => { editorTabs.OpenTab(description, log); };
        }

        public static Icon GetItemIcon(ItemDescription.ItemType type, ILogger log)
        {
            return GetIcon(type.ToString(), log);
        }
        
        public static Icon GetIcon(string iconName, ILogger log, int size = 16)
        {
            try
            {
                return Icon.FromResource($"SerialLoops.Icons.{iconName}.png").WithSize(size, size);
            }
            catch (Exception exc)
            {
                log.LogWarning($"Failed to load icon.\n{exc.Message}\n\n{exc.StackTrace}");
                return null;
            }
        }

        public static StackLayout GetControlWithIcon(Control control, string iconName, ILogger log)
        {
            return new StackLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
                Spacing = 5,
                Items =
                {
                    new ImageView { Image = new Bitmap(GetIcon(iconName, log)) },
                    control
                }
            };
        }

        public static ButtonToolItem GetToolBarItem(Command command)
        {
            return new(command) { Style = "sl-toolbar-button" };
        }

        internal static StackLayout GetPlayerStackLayout(SoundPlayerPanel soundPlayer, Control trackName, string trackDetails, short? trackId = null)
        {
            StackLayout details = new()
            {
                Orientation = Orientation.Vertical,
                Spacing = 5,
            };
            if (trackName is not null)
            {
                details.Items.Add(trackName);
            }
            StackLayout extraDetails = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
            };
            if (!string.IsNullOrEmpty(trackDetails))
            {
                extraDetails.Items.Add(trackDetails);
            }
            if (trackId is not null)
            {
                extraDetails.Items.Add($"{trackId}");
            }
            details.Items.Add(extraDetails);

            return new StackLayout
            {
                Orientation = Orientation.Horizontal,
                VerticalContentAlignment = VerticalAlignment.Center,
                Spacing = 10,
                Items =
                {
                    soundPlayer,
                    details
                }
            };
        }

        internal static Label GetTextHeader(string text, int size = 14)
        {
            return new Label { Text = text, Font = new Font(SystemFont.Bold, size) };
        }
    }
}
