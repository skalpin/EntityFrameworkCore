// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class EntityProjectionExpression : Expression
    {
        private readonly IDictionary<IProperty, ColumnExpression> _propertyExpressionsCache
            = new Dictionary<IProperty, ColumnExpression>();
        private readonly IDictionary<INavigation, EntityShaperExpression> _navigationExpressionsCache
            = new Dictionary<INavigation, EntityShaperExpression>();

        private readonly bool _nullable;

        public EntityProjectionExpression(IEntityType entityType, TableExpressionBase innerTable, bool nullable)
        {
            EntityType = entityType;
            InnerTable = innerTable;
            _nullable = nullable;
        }

        public EntityProjectionExpression(IEntityType entityType, IDictionary<IProperty, ColumnExpression> propertyExpressions)
        {
            EntityType = entityType;
            _propertyExpressionsCache = propertyExpressions;
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            if (InnerTable != null)
            {
                var table = (TableExpressionBase)visitor.Visit(InnerTable);

                return table != InnerTable
                    ? new EntityProjectionExpression(EntityType, table, _nullable)
                    : this;
            }

            var changed = false;
            var newCache = new Dictionary<IProperty, ColumnExpression>();
            foreach (var expression in _propertyExpressionsCache)
            {
                var newExpression = (ColumnExpression)visitor.Visit(expression.Value);
                changed |= newExpression != expression.Value;

                newCache[expression.Key] = newExpression;
            }

            return changed
                ? new EntityProjectionExpression(EntityType, newCache)
                : this;
        }

        public virtual EntityProjectionExpression MakeNullable()
        {
            if (InnerTable != null)
            {
                return new EntityProjectionExpression(EntityType, InnerTable, true);
            }

            var newCache = new Dictionary<IProperty, ColumnExpression>();
            foreach (var expression in _propertyExpressionsCache)
            {
                newCache[expression.Key] = expression.Value.MakeNullable();
            }

            return new EntityProjectionExpression(EntityType, newCache);
        }

        public virtual EntityProjectionExpression UpdateEntityType(IEntityType derivedType)
        {
            if (InnerTable != null)
            {
                return new EntityProjectionExpression(derivedType, InnerTable, _nullable);
            }
            else
            {
                var propertyExpressionCache = new Dictionary<IProperty, ColumnExpression>();
                foreach (var kvp in _propertyExpressionsCache)
                {
                    var property = kvp.Key;
                    if (derivedType.IsAssignableFrom(property.DeclaringEntityType)
                        || property.DeclaringEntityType.IsAssignableFrom(derivedType))
                    {
                        propertyExpressionCache[property] = kvp.Value;
                    }
                }

                return new EntityProjectionExpression(derivedType, propertyExpressionCache);
            }
        }

        public virtual TableExpressionBase InnerTable { get; }
        public virtual IEntityType EntityType { get; }
        public sealed override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => EntityType.ClrType;

        public virtual ColumnExpression BindProperty(IProperty property)
        {
            if (!EntityType.IsAssignableFrom(property.DeclaringEntityType)
                && !property.DeclaringEntityType.IsAssignableFrom(EntityType))
            {
                throw new InvalidOperationException(
                    $"Called EntityProjectionExpression.BindProperty() with incorrect IProperty. EntityType:{EntityType.DisplayName()}, Property:{property.Name}");
            }

            if (!_propertyExpressionsCache.TryGetValue(property, out var expression))
            {
                expression = new ColumnExpression(property, InnerTable, _nullable);
                _propertyExpressionsCache[property] = expression;
            }

            return expression;
        }

        public virtual void AddNavigationBinding(INavigation navigation, EntityShaperExpression entityShaper)
        {
            if (!EntityType.IsAssignableFrom(navigation.DeclaringEntityType)
                && !navigation.DeclaringEntityType.IsAssignableFrom(EntityType))
            {
                throw new InvalidOperationException(
                    $"Called EntityProjectionExpression.AddNavigationBinding() with incorrect INavigation. " +
                    $"EntityType:{EntityType.DisplayName()}, Property:{navigation.Name}");
            }

            _navigationExpressionsCache[navigation] = entityShaper;
        }

        public virtual EntityShaperExpression BindNavigation(INavigation navigation)
        {
            if (!EntityType.IsAssignableFrom(navigation.DeclaringEntityType)
                && !navigation.DeclaringEntityType.IsAssignableFrom(EntityType))
            {
                throw new InvalidOperationException(
                    $"Called EntityProjectionExpression.BindNavigation() with incorrect INavigation. " +
                    $"EntityType:{EntityType.DisplayName()}, Property:{navigation.Name}");
            }

            return _navigationExpressionsCache.TryGetValue(navigation, out var expression)
                ? expression
                : null;
        }
    }
}
