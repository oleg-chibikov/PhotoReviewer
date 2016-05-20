using System;
using System.Linq.Expressions;
using System.Windows;
using JetBrains.Annotations;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable IntroduceOptionalParameters.Global

namespace PhotoReviewer
{
    public static class DependencyProperty<T> where T : DependencyObject
    {
        [NotNull]
        public static DependencyProperty Register<TProperty>([NotNull] Expression<Func<T, TProperty>> propertyExpression)
        {
            return Register(propertyExpression, default(TProperty), null);
        }

        [NotNull]
        public static DependencyProperty Register<TProperty>([NotNull] Expression<Func<T, TProperty>> propertyExpression,
            TProperty defaultValue)
        {
            return Register(propertyExpression, defaultValue, null);
        }

        [NotNull]
        public static DependencyProperty Register<TProperty>([NotNull] Expression<Func<T, TProperty>> propertyExpression,
            [NotNull] Func<T, PropertyChangedCallback<TProperty>> propertyChangedCallbackFunc)
        {
            return Register(propertyExpression, default(TProperty), propertyChangedCallbackFunc);
        }

        [NotNull]
        public static DependencyProperty Register<TProperty>([NotNull] Expression<Func<T, TProperty>> propertyExpression,
            [CanBeNull] TProperty defaultValue, [CanBeNull] Func<T, PropertyChangedCallback<TProperty>> propertyChangedCallbackFunc)
        {
            var propertyName = propertyExpression.RetrieveMemberName();
            var callback = ConvertCallback(propertyChangedCallbackFunc);

            return DependencyProperty.Register(propertyName, typeof(TProperty), typeof(T),
                new PropertyMetadata(defaultValue, callback));
        }

        [CanBeNull]
        private static PropertyChangedCallback ConvertCallback<TProperty>([CanBeNull] Func<T, PropertyChangedCallback<TProperty>> propertyChangedCallbackFunc)
        {
            if (propertyChangedCallbackFunc == null)
                return null;
            return (d, e) =>
            {
                var callback = propertyChangedCallbackFunc((T)d);
                callback?.Invoke(new DependencyPropertyChangedEventArgs<TProperty>(e));
            };
        }
    }

    [NotNull]
    public delegate void PropertyChangedCallback<TProperty>(DependencyPropertyChangedEventArgs<TProperty> e);

    public class DependencyPropertyChangedEventArgs<T> : EventArgs
    {
        public DependencyPropertyChangedEventArgs(DependencyPropertyChangedEventArgs e)
        {
            NewValue = (T)e.NewValue;
            OldValue = (T)e.OldValue;
            Property = e.Property;
        }

        [NotNull]
        public T NewValue { get; private set; }

        [NotNull]
        public T OldValue { get; private set; }

        [NotNull]
        public DependencyProperty Property { get; private set; }
    }

    public static class ExpressionExtensions
    {
        [NotNull]
        public static string RetrieveMemberName<TArg, TRes>([NotNull] this Expression<Func<TArg, TRes>> propertyExpression)
        {
            var memberExpression = propertyExpression.Body as MemberExpression;
            if (memberExpression == null)
            {
                var unaryExpression = propertyExpression.Body as UnaryExpression;
                if (unaryExpression != null)
                    memberExpression = unaryExpression.Operand as MemberExpression;
            }
            var parameterExpression = memberExpression?.Expression as ParameterExpression;
            if (parameterExpression != null && parameterExpression.Name == propertyExpression.Parameters[0].Name)
                return memberExpression.Member.Name;
            throw new ArgumentException("Invalid expression.", nameof(propertyExpression));
        }
    }
}