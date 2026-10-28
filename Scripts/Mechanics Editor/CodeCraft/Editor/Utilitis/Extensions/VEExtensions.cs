using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HSE.CodeCraft.Editor
{
    public static class VEExtensions
    {
        public static void RegisterOnPropertyValueChangeCallback<T>(this VisualElement element, PropertyField property = null, Action<T> actionOnProperty = null)
        {
            property?.RegisterValueChangeCallback((evt) =>
            {
                T value = evt.changedProperty != null ? (T)evt.changedProperty.boxedValue : default(T);
                actionOnProperty?.Invoke(value);
            });
        }
        public static void RegisterImagePropertyChangeCallback(this Image image, PropertyField property = null)
        {
            image.RegisterOnPropertyValueChangeCallback<Texture2D>(property, (tex) =>
            {
                if (tex == null) return;
                image.image = tex;
                image.SetSize(tex.width / 3, tex.height / 3);
            });
        }

        public static IntegerField AddBindIntElement(this VisualElement element, float width, float height)
        {
            throw new NotImplementedException();
        }
        public static Button AddButtonElement(this VisualElement element, float width, float height, string buttonText = "Text", Action onClickAction = null, float styleLength = 8f)
        {
            Button button = new Button(() => onClickAction?.Invoke());
            button.SetSize(width, height);
            button.text = buttonText;
            button.style.fontSize = new StyleLength(styleLength);

            element.Add(button);
            return button;
        }

        public static PopupField<T> AddPopup<T>(this VisualElement element, List<T> choises, T defaullValue = default)
        {
            var popup = new PopupField<T>(choises,defaullValue);
            element.Add(popup);
            return popup;
        }

        public static Image AddImageElement(this VisualElement element, Align align = Align.Center)
        {
            Image img = new Image();
            img.style.alignSelf = new StyleEnum<Align>(align);
            element.Add(img);
            return img;
        }


        public static Image AddResourceImageWithBorderStyle(this VisualElement element,string resourceIconName, Color? iconColor = null,  float size = 12f, float borderSizeDivider = 5)
        {
            if (string.IsNullOrEmpty(resourceIconName))
                return null;

            Texture tex = Resources.Load<Texture2D>(resourceIconName);
            return AddImageWithBorderStyle(element, tex, resourceIconName, iconColor, size, borderSizeDivider);
        }

        public static Image AddImageWithBorderStyle(this VisualElement element, Texture texture, string iconName = "", Color? iconColor = null, float size = 12f, float borderSizeDivider = 5)
        {
            if (texture == null) return null;

            iconColor ??= Color.white;
            iconName = string.IsNullOrEmpty(iconName) ? texture.name: iconName;

            Image image = element.AddImageElement();
            image.name = iconName;
            image.image = texture;
            image.tintColor = iconColor.Value;
            image.SetSquareSize(size);
            image.RoundOffStyle(borderSizeDivider);
            return image;
        }

        public static VisualElement SetBackgroundImage(this VisualElement element,string resourceIconPath, float size = 7, Color imageColor = default)
        {
            imageColor = imageColor == default ? Color.white : imageColor;
            element.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>(resourceIconPath));
            element.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(size, size));
            element.style.unityBackgroundImageTintColor = imageColor;
            return element;
        }
        public static VisualElement SetBackgroundColor(this VisualElement element, Color bgColor = default)
        {
            bgColor = bgColor == default ? Color.white : bgColor;
            element.style.backgroundColor = bgColor;
            return element;
        }
        public static VisualElement SetSquareSize(this VisualElement element, float size, LengthUnit lengthUnit = LengthUnit.Pixel)=> element.SetSize(size, size);
        public static VisualElement SetSize(this VisualElement element, float width, float height, LengthUnit lengthUnit = LengthUnit.Pixel)
        {
            element.style.width = new Length(width, lengthUnit);
            element.style.height = new Length(height, lengthUnit);
            return element;
        }
        public static VisualElement RoundOffStyle(this VisualElement element, float subdivider, LengthUnit lengthUnit = LengthUnit.Pixel)=> element.RoundOffStyle(subdivider, subdivider, subdivider, subdivider);
        public static VisualElement RoundOffStyle(this VisualElement element, float subdividerTopLeft, float subdividerTopRight, float subdividerBottomLeft, float subdividerBottomRight, LengthUnit lengthUnit = LengthUnit.Pixel)
        {
            element.style.borderTopLeftRadius = new Length(subdividerTopLeft, lengthUnit);
            element.style.borderTopRightRadius = new Length(subdividerTopRight, lengthUnit);
            element.style.borderBottomLeftRadius = new Length(subdividerBottomLeft, lengthUnit);
            element.style.borderBottomRightRadius = new Length(subdividerBottomRight, lengthUnit);
            return element;
        }

        public static Color GradientColorTransition(float time, Color start, Color end)
        {
            return Color.Lerp(start, end, time);
        }
    }
}
