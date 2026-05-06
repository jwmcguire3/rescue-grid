#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !CAPTURE_BUILD
using UnityEngine;
using UnityEngine.UIElements;

namespace Rescue.Unity.Debugging
{
    internal static class DebugPanelFallbackUi
    {
        public static Label MakeHeader(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("debug-title");
            return label;
        }

        public static Label MakeSection(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("debug-section");
            return label;
        }

        public static VisualElement MakeRow(out Label label, string name, string text = "")
        {
            label = new Label(text) { name = name };
            label.AddToClassList("debug-value");
            return label;
        }

        public static VisualElement MakeFieldRow(string title, out DropdownField field, string name)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("field-row");
            row.Add(new Label(title));
            field = new DropdownField { name = name };
            field.style.flexGrow = 1.0f;
            row.Add(field);
            return row;
        }

        public static VisualElement MakeFieldRow(string title, out IntegerField field, string name)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("field-row");
            row.Add(new Label(title));
            field = new IntegerField { name = name };
            field.style.flexGrow = 1.0f;
            row.Add(field);
            return row;
        }

        public static VisualElement MakeFieldRow(string title, out TextField field, string name)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("field-row");
            row.Add(new Label(title));
            field = new TextField { name = name };
            field.style.flexGrow = 1.0f;
            row.Add(field);
            return row;
        }

        public static VisualElement MakeFloatFieldRow(string title, out FloatField field, string name)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("field-row");
            row.Add(new Label(title));
            field = new FloatField { name = name };
            field.style.flexGrow = 1.0f;
            row.Add(field);
            return row;
        }

        public static VisualElement MakeVector3FieldRow(string title, out Vector3Field field, string name)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("field-row");
            row.Add(new Label(title));
            field = new Vector3Field { name = name };
            field.style.flexGrow = 1.0f;
            row.Add(field);
            return row;
        }

        public static VisualElement MakeSpeedSliderRow(
            string title,
            out Slider slider,
            out Label valueLabel,
            string sliderName,
            string valueName,
            float lowValue,
            float highValue,
            string defaultValueText)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("field-row");
            row.AddToClassList("speed-slider-row");
            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("speed-slider-title");
            row.Add(titleLabel);
            slider = new Slider(lowValue, highValue)
            {
                name = sliderName,
            };
            slider.style.flexGrow = 1.0f;
            row.Add(slider);
            valueLabel = new Label(defaultValueText) { name = valueName };
            valueLabel.AddToClassList("speed-slider-value");
            row.Add(valueLabel);
            return row;
        }

        public static Button MakeButton(string text, string name, out Button button)
        {
            button = new Button { text = text, name = name };
            return button;
        }
    }
}
#endif
