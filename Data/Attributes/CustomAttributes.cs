﻿using System;

namespace MVCCoreVue.Data.Attributes
{
    internal class DefaultAttribute : Attribute
    {
        public object Default { get; set; }

        public DefaultAttribute(object defaultValue)
        {
            Default = defaultValue;
        }
    }

    internal class DefaultPermissionAttribute : Attribute
    {
        public bool HasDefaultAllPermissions { get; set; }
        public string DefaultPermissions { get; set; }
    }

    internal class HelpAttribute : Attribute
    {
        public string HelpText { get; set; }

        public HelpAttribute(string helpText)
        {
            HelpText = helpText;
        }
    }

    internal class HiddenAttribute : Attribute
    {
        public bool Hidden { get; set; }

        public bool HideInTable { get; set; }

        public HiddenAttribute(bool hidden = true)
        {
            Hidden = hidden;
        }
    }

    internal class IconAttribute : Attribute
    {
        public string Icon { get; set; }

        public IconAttribute(string icon) { Icon = icon; }
    }

    /// <summary>
    /// Classes with this attribute will appear in the site menu.
    /// Classes without it will not appear, and so will not be directly
    /// accessible on the site unless they appear as nested data within
    /// other classes.
    /// </summary>
    internal class MenuClassAttribute : Attribute
    {
        /// <summary>
        /// A string with the format "supertype/type/subtype"
        /// </summary>
        /// <remarks>
        /// Used to generate menu and URL structure, doesn't need to reflect
        /// data relationships in any way.
        /// </remarks>
        public string Category { get; set; }

        /// <summary>
        /// The name of a Material Icon, will appear as the class's icon in the
        /// menu, and will also be the icon of every category above the class if
        /// it is the first item added to that category.
        /// </summary>
        /// <remarks>Spaces should be replaced by underscores in icon names.</remarks>
        public string IconClass { get; set; }

        public MenuClassAttribute() { }
    }

    internal class RowsAttribute : Attribute
    {
        public int Rows { get; set; }

        public RowsAttribute(int rows)
        {
            Rows = rows;
        }
    }

    internal class StepAttribute : Attribute
    {
        public double Step { get; set; }

        public StepAttribute(double step)
        {
            Step = step;
        }
    }

    internal class TextAttribute : Attribute
    {
        public string Prefix { get; set; }
        public string Suffix { get; set; }

        public TextAttribute() { }
    }

    internal class ValidatorAttribute : Attribute
    {
        public string Validator { get; set; }

        public ValidatorAttribute(string validator)
        {
            Validator = validator;
        }
    }
}