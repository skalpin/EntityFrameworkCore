﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class SqlExpressionFactory : ISqlExpressionFactory
    {
        private readonly IRelationalTypeMappingSource _typeMappingSource;
        private readonly RelationalTypeMapping _boolTypeMapping;

        public SqlExpressionFactory(SqlExpressionFactoryDependencies dependencies)
        {
            _typeMappingSource = dependencies.TypeMappingSource;
            _boolTypeMapping = _typeMappingSource.FindMapping(typeof(bool));
        }

        public virtual SqlExpression ApplyDefaultTypeMapping(SqlExpression sqlExpression)
            => sqlExpression == null || sqlExpression.TypeMapping != null
                ? sqlExpression
                : ApplyTypeMapping(sqlExpression, _typeMappingSource.FindMapping(sqlExpression.Type));

        public virtual SqlExpression ApplyTypeMapping(SqlExpression sqlExpression, RelationalTypeMapping typeMapping)
        {
            if (sqlExpression == null
                || sqlExpression.TypeMapping != null)
            {
                return sqlExpression;
            }

            return sqlExpression switch
            {
                CaseExpression e         => ApplyTypeMappingOnCase(e, typeMapping),
                LikeExpression e         => ApplyTypeMappingOnLike(e),
                SqlBinaryExpression e    => ApplyTypeMappingOnSqlBinary(e, typeMapping),
                SqlUnaryExpression e     => ApplyTypeMappingOnSqlUnary(e, typeMapping),
                SqlConstantExpression e  => e.ApplyTypeMapping(typeMapping),
                SqlFragmentExpression e  => e,
                SqlFunctionExpression e  => e.ApplyTypeMapping(typeMapping),
                SqlParameterExpression e => e.ApplyTypeMapping(typeMapping),
                _                        => sqlExpression
            };
        }

        private SqlExpression ApplyTypeMappingOnLike(LikeExpression likeExpression)
        {
            var inferredTypeMapping = ExpressionExtensions.InferTypeMapping(
                likeExpression.Match, likeExpression.Pattern, likeExpression.EscapeChar)
                ?? _typeMappingSource.FindMapping(likeExpression.Match.Type);

            return new LikeExpression(
                ApplyTypeMapping(likeExpression.Match, inferredTypeMapping),
                ApplyTypeMapping(likeExpression.Pattern, inferredTypeMapping),
                ApplyTypeMapping(likeExpression.EscapeChar, inferredTypeMapping),
                _boolTypeMapping);
        }

        private SqlExpression ApplyTypeMappingOnCase(
            CaseExpression caseExpression, RelationalTypeMapping typeMapping)
        {
            var whenClauses = new List<CaseWhenClause>();
            foreach (var caseWhenClause in caseExpression.WhenClauses)
            {
                whenClauses.Add(
                    new CaseWhenClause(
                        caseWhenClause.Test,
                        ApplyTypeMapping(caseWhenClause.Result, typeMapping)));
            }

            var elseResult = ApplyTypeMapping(caseExpression.ElseResult, typeMapping);

            return caseExpression.Update(caseExpression.Operand, whenClauses, elseResult);
        }

        private SqlExpression ApplyTypeMappingOnSqlUnary(
            SqlUnaryExpression sqlUnaryExpression, RelationalTypeMapping typeMapping)
        {
            SqlExpression operand;
            RelationalTypeMapping resultTypeMapping;
            switch (sqlUnaryExpression.OperatorType)
            {
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.Not:
                    resultTypeMapping = _boolTypeMapping;
                    operand = ApplyDefaultTypeMapping(sqlUnaryExpression.Operand);
                    break;

                case ExpressionType.Convert:
                    resultTypeMapping = typeMapping;
                    operand = ApplyDefaultTypeMapping(sqlUnaryExpression.Operand);
                    break;

                case ExpressionType.Negate:
                    resultTypeMapping = typeMapping;
                    operand = ApplyTypeMapping(sqlUnaryExpression.Operand, typeMapping);
                    break;

                default:
                    throw new InvalidOperationException();
            }

            return new SqlUnaryExpression(
                sqlUnaryExpression.OperatorType,
                operand,
                sqlUnaryExpression.Type,
                resultTypeMapping);
        }

        private SqlExpression ApplyTypeMappingOnSqlBinary(
            SqlBinaryExpression sqlBinaryExpression, RelationalTypeMapping typeMapping)
        {
            var left = sqlBinaryExpression.Left;
            var right = sqlBinaryExpression.Right;

            Type resultType;
            RelationalTypeMapping resultTypeMapping;
            RelationalTypeMapping inferredTypeMapping;
            switch (sqlBinaryExpression.OperatorType)
            {
                case ExpressionType.Equal:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.NotEqual:
                {
                    inferredTypeMapping = ExpressionExtensions.InferTypeMapping(left, right)
                        ?? _typeMappingSource.FindMapping(left.Type);
                    resultType = typeof(bool);
                    resultTypeMapping = _boolTypeMapping;
                }
                break;

                case ExpressionType.AndAlso:
                case ExpressionType.OrElse:
                {
                    inferredTypeMapping = _boolTypeMapping;
                    resultType = typeof(bool);
                    resultTypeMapping = _boolTypeMapping;
                }
                break;

                case ExpressionType.Add:
                case ExpressionType.Subtract:
                case ExpressionType.Multiply:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.Coalesce:
                case ExpressionType.And:
                case ExpressionType.Or:
                {
                    inferredTypeMapping = typeMapping ?? ExpressionExtensions.InferTypeMapping(left, right);
                    resultType = left.Type;
                    resultTypeMapping = inferredTypeMapping;
                }
                break;

                default:
                    throw new InvalidOperationException("Incorrect operatorType for SqlBinaryExpression");
            }

            return new SqlBinaryExpression(
                sqlBinaryExpression.OperatorType,
                ApplyTypeMapping(left, inferredTypeMapping),
                ApplyTypeMapping(right, inferredTypeMapping),
                resultType,
                resultTypeMapping);
        }

        public virtual RelationalTypeMapping GetTypeMappingForValue(object value)
            => _typeMappingSource.GetMappingForValue(value);

        public virtual RelationalTypeMapping FindMapping(Type type)
            => _typeMappingSource.FindMapping(type);

        public virtual SqlBinaryExpression MakeBinary(
            ExpressionType operatorType, SqlExpression left, SqlExpression right, RelationalTypeMapping typeMapping)
        {
            var returnType = left.Type;
            switch (operatorType)
            {
                case ExpressionType.Equal:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.NotEqual:
                case ExpressionType.AndAlso:
                case ExpressionType.OrElse:
                    returnType = typeof(bool);
                    break;
            }

            return (SqlBinaryExpression)ApplyTypeMapping(
                new SqlBinaryExpression(operatorType, left, right, returnType, null), typeMapping);
        }

        public virtual SqlBinaryExpression Equal(SqlExpression left, SqlExpression right)
            => MakeBinary(ExpressionType.Equal, left, right, null);

        public virtual SqlBinaryExpression NotEqual(SqlExpression left, SqlExpression right)
            => MakeBinary(ExpressionType.NotEqual, left, right, null);

        public virtual SqlBinaryExpression GreaterThan(SqlExpression left, SqlExpression right)
            => MakeBinary(ExpressionType.GreaterThan, left, right, null);

        public virtual SqlBinaryExpression GreaterThanOrEqual(SqlExpression left, SqlExpression right)
            => MakeBinary(ExpressionType.GreaterThanOrEqual, left, right, null);

        public virtual SqlBinaryExpression LessThan(SqlExpression left, SqlExpression right)
            => MakeBinary(ExpressionType.LessThan, left, right, null);

        public virtual SqlBinaryExpression LessThanOrEqual(SqlExpression left, SqlExpression right)
            => MakeBinary(ExpressionType.LessThanOrEqual, left, right, null);

        public virtual SqlBinaryExpression AndAlso(SqlExpression left, SqlExpression right)
            => MakeBinary(ExpressionType.AndAlso, left, right, null);

        public virtual SqlBinaryExpression OrElse(SqlExpression left, SqlExpression right)
            => MakeBinary(ExpressionType.OrElse, left, right, null);

        public virtual SqlBinaryExpression Add(SqlExpression left, SqlExpression right, RelationalTypeMapping typeMapping = null)
            => MakeBinary(ExpressionType.Add, left, right, typeMapping);

        public virtual SqlBinaryExpression Subtract(SqlExpression left, SqlExpression right, RelationalTypeMapping typeMapping = null)
            => MakeBinary(ExpressionType.Subtract, left, right, typeMapping);

        public virtual SqlBinaryExpression Multiply(SqlExpression left, SqlExpression right, RelationalTypeMapping typeMapping = null)
            => MakeBinary(ExpressionType.Multiply, left, right, typeMapping);

        public virtual SqlBinaryExpression Divide(SqlExpression left, SqlExpression right, RelationalTypeMapping typeMapping = null)
            => MakeBinary(ExpressionType.Divide, left, right, typeMapping);

        public virtual SqlBinaryExpression Modulo(SqlExpression left, SqlExpression right, RelationalTypeMapping typeMapping = null)
            => MakeBinary(ExpressionType.Modulo, left, right, typeMapping);

        public virtual SqlBinaryExpression And(SqlExpression left, SqlExpression right, RelationalTypeMapping typeMapping = null)
            => MakeBinary(ExpressionType.And, left, right, typeMapping);

        public virtual SqlBinaryExpression Or(SqlExpression left, SqlExpression right, RelationalTypeMapping typeMapping = null)
            => MakeBinary(ExpressionType.Or, left, right, typeMapping);

        public virtual SqlBinaryExpression Coalesce(SqlExpression left, SqlExpression right, RelationalTypeMapping typeMapping = null)
            => MakeBinary(ExpressionType.Coalesce, left, right, typeMapping);

        public virtual SqlUnaryExpression MakeUnary(ExpressionType operatorType, SqlExpression operand, Type type, RelationalTypeMapping typeMapping = null)
            => (SqlUnaryExpression)ApplyTypeMapping(new SqlUnaryExpression(operatorType, operand, type, null), typeMapping);

        public virtual SqlUnaryExpression IsNull(SqlExpression operand)
            => MakeUnary(ExpressionType.Equal, operand, typeof(bool));

        public virtual SqlUnaryExpression IsNotNull(SqlExpression operand)
            => MakeUnary(ExpressionType.NotEqual, operand, typeof(bool));

        public virtual SqlUnaryExpression Convert(SqlExpression operand, Type type, RelationalTypeMapping typeMapping = null)
            => MakeUnary(ExpressionType.Convert, operand, type, typeMapping);

        public virtual SqlUnaryExpression Not(SqlExpression operand)
            => MakeUnary(ExpressionType.Not, operand, typeof(bool));

        public virtual SqlUnaryExpression Negate(SqlExpression operand)
            => MakeUnary(ExpressionType.Negate, operand, operand.Type, operand.TypeMapping);

        public virtual CaseExpression Case(SqlExpression operand, SqlExpression elseResult, params CaseWhenClause[] whenClauses)
        {
            var operandTypeMapping = operand.TypeMapping
                ?? whenClauses.Select(wc => wc.Test.TypeMapping).FirstOrDefault(t => t != null)
                ?? _typeMappingSource.FindMapping(operand.Type);

            var resultTypeMapping = elseResult?.TypeMapping
                ?? whenClauses.Select(wc => wc.Result.TypeMapping).FirstOrDefault(t => t != null);

            operand = ApplyTypeMapping(operand, operandTypeMapping);

            var typeMappedWhenClauses = new List<CaseWhenClause>();
            foreach (var caseWhenClause in whenClauses)
            {
                typeMappedWhenClauses.Add(
                    new CaseWhenClause(
                        ApplyTypeMapping(caseWhenClause.Test, operandTypeMapping),
                        ApplyTypeMapping(caseWhenClause.Result, resultTypeMapping)));
            }

            elseResult = ApplyTypeMapping(elseResult, resultTypeMapping);

            return new CaseExpression(operand, typeMappedWhenClauses, elseResult);
        }

        public virtual CaseExpression Case(SqlExpression operand, params CaseWhenClause[] whenClauses)
            => Case(operand, null, whenClauses);

        public virtual CaseExpression Case(IReadOnlyList<CaseWhenClause> whenClauses, SqlExpression elseResult)
        {
            var resultTypeMapping = elseResult?.TypeMapping
                ?? whenClauses.Select(wc => wc.Result.TypeMapping).FirstOrDefault(t => t != null);

            var typeMappedWhenClauses = new List<CaseWhenClause>();
            foreach (var caseWhenClause in whenClauses)
            {
                typeMappedWhenClauses.Add(
                    new CaseWhenClause(
                        ApplyTypeMapping(caseWhenClause.Test, _boolTypeMapping),
                        ApplyTypeMapping(caseWhenClause.Result, resultTypeMapping)));
            }

            elseResult = ApplyTypeMapping(elseResult, resultTypeMapping);

            return new CaseExpression(typeMappedWhenClauses, elseResult);
        }

        public virtual SqlFunctionExpression Function(
            string name, IEnumerable<SqlExpression> arguments, Type returnType, RelationalTypeMapping typeMapping = null)
        {
            var typeMappedArguments = new List<SqlExpression>();

            foreach (var argument in arguments)
            {
                typeMappedArguments.Add(ApplyDefaultTypeMapping(argument));
            }

            return SqlFunctionExpression.Create(
                name,
                typeMappedArguments,
                returnType,
                typeMapping);
        }

        public virtual SqlFunctionExpression Function(
            string schema, string name, IEnumerable<SqlExpression> arguments, Type returnType, RelationalTypeMapping typeMapping = null)
        {
            var typeMappedArguments = new List<SqlExpression>();
            foreach (var argument in arguments)
            {
                typeMappedArguments.Add(ApplyDefaultTypeMapping(argument));
            }

            return SqlFunctionExpression.Create(
                schema,
                name,
                typeMappedArguments,
                returnType,
                typeMapping);
        }

        public virtual SqlFunctionExpression Function(
            SqlExpression instance, string name, IEnumerable<SqlExpression> arguments, Type returnType, RelationalTypeMapping typeMapping = null)
        {
            instance = ApplyDefaultTypeMapping(instance);
            var typeMappedArguments = new List<SqlExpression>();
            foreach (var argument in arguments)
            {
                typeMappedArguments.Add(ApplyDefaultTypeMapping(argument));
            }

            return SqlFunctionExpression.Create(
                instance,
                name,
                typeMappedArguments,
                returnType,
                typeMapping);
        }

        public virtual SqlFunctionExpression Function(string name, Type returnType, RelationalTypeMapping typeMapping = null)
            => SqlFunctionExpression.CreateNiladic(name, returnType, typeMapping);

        public virtual SqlFunctionExpression Function(string schema, string name, Type returnType, RelationalTypeMapping typeMapping = null)
            => SqlFunctionExpression.CreateNiladic(schema, name, returnType, typeMapping);

        public virtual SqlFunctionExpression Function(SqlExpression instance, string name, Type returnType, RelationalTypeMapping typeMapping = null)
            => SqlFunctionExpression.CreateNiladic(ApplyDefaultTypeMapping(instance), name, returnType, typeMapping);

        public virtual ExistsExpression Exists(SelectExpression subquery, bool negated)
            => new ExistsExpression(subquery, negated, _boolTypeMapping);

        public virtual InExpression In(SqlExpression item, SqlExpression values, bool negated)
        {
            var typeMapping = item.TypeMapping ?? _typeMappingSource.FindMapping(item.Type);

            item = ApplyTypeMapping(item, typeMapping);
            values = ApplyTypeMapping(values, typeMapping);

            return new InExpression(item, negated, values, _boolTypeMapping);
        }

        public virtual InExpression In(SqlExpression item, SelectExpression subquery, bool negated)
        {
            var typeMapping = subquery.Projection.Single().Expression.TypeMapping;

            if (typeMapping == null)
            {
                throw new InvalidOperationException();
            }

            item = ApplyTypeMapping(item, typeMapping);
            return new InExpression(item, negated, subquery, _boolTypeMapping);
        }

        public virtual LikeExpression Like(SqlExpression match, SqlExpression pattern, SqlExpression escapeChar = null)
            => (LikeExpression)ApplyDefaultTypeMapping(new LikeExpression(match, pattern, escapeChar, null));

        public virtual SqlFragmentExpression Fragment(string sql)
            => new SqlFragmentExpression(sql);

        public virtual SqlConstantExpression Constant(object value, RelationalTypeMapping typeMapping = null)
            => new SqlConstantExpression(Expression.Constant(value), typeMapping);

        public virtual SelectExpression Select(SqlExpression projection)
        {
            var selectExpression = new SelectExpression(
                alias: null,
                new List<ProjectionExpression>(),
                new List<TableExpressionBase>(),
                new List<SqlExpression>(),
                new List<OrderingExpression>());

            if (projection != null)
            {
                selectExpression.ReplaceProjectionMapping(new Dictionary<ProjectionMember, Expression>
                {
                    { new ProjectionMember(), projection }
                });
            }

            return selectExpression;
        }

        public virtual SelectExpression Select(IEntityType entityType)
        {
            var selectExpression = new SelectExpression(entityType);
            AddConditions(selectExpression, entityType);

            return selectExpression;
        }

        public virtual SelectExpression Select(IEntityType entityType, string sql, Expression sqlArguments)
        {
            var selectExpression = new SelectExpression(entityType, sql, sqlArguments);
            AddConditions(selectExpression, entityType);

            return selectExpression;
        }

        private void AddConditions(SelectExpression selectExpression, IEntityType entityType)
        {
            SqlExpression predicate;
            if (entityType.FindPrimaryKey() == null)
            {
                predicate = GenerateDiscriminatorExpression(selectExpression, entityType, null);
            }
            else
            {
                var sharingTypes = new HashSet<IEntityType>(
                        entityType.Model.GetEntityTypes()
                            .Where(et => et.FindPrimaryKey() != null
                                      && et.GetTableName() == entityType.GetTableName()
                                      && et.GetSchema() == entityType.GetSchema()));

                var currentPath = new Stack<IEntityType>();
                currentPath.Push(entityType);

                var allPaths = new List<List<IEntityType>>();
                FindPaths(entityType.GetRootType(), sharingTypes, currentPath, allPaths);

                predicate = allPaths
                    .Select(
                        p => p.Select(
                                et => GenerateDiscriminatorExpression(selectExpression, et, sharingTypes)
                                   ?? GenerateSharedTableExpression(selectExpression, et, sharingTypes))
                            .Aggregate(
                                (SqlExpression)null,
                                (result, current) => result != null
                                    ? current != null
                                        ? AndAlso(result, current)
                                        : result
                                    : current))
                    .Aggregate(
                        (SqlExpression)null,
                        (result, current) => result != null
                            ? current != null
                                ? OrElse(result, current)
                                : result
                            : current);
            }

            if (predicate != null)
            {
                selectExpression.ApplyPredicate(predicate);
            }
        }

        private void FindPaths(
            IEntityType entityType, ICollection<IEntityType> sharingTypes,
            Stack<IEntityType> currentPath, ICollection<List<IEntityType>> result)
        {
            var identifyingFks = entityType.FindForeignKeys(entityType.FindPrimaryKey().Properties)
                .Where(
                    fk => fk.PrincipalKey.IsPrimaryKey()
                          && fk.PrincipalEntityType != entityType
                          && sharingTypes.Contains(fk.PrincipalEntityType))
                .ToList();

            if (identifyingFks.Count == 0)
            {
                result.Add(new List<IEntityType>(currentPath));
                return;
            }

            foreach (var fk in identifyingFks)
            {
                currentPath.Push(fk.PrincipalEntityType);
                FindPaths(fk.PrincipalEntityType.GetRootType(), sharingTypes, currentPath, result);
                currentPath.Pop();
            }
        }

        private SqlExpression GenerateDiscriminatorExpression(
            SelectExpression selectExpression, IEntityType entityType, HashSet<IEntityType> sharingTypes)
        {
            var concreteEntityTypes = entityType.GetConcreteDerivedTypesInclusive().ToList();
            if (concreteEntityTypes.Count == 1)
            {
                var concreteEntityType = concreteEntityTypes[0];
                // If not a derived type
                if (concreteEntityType.GetRootType() == concreteEntityType)
                {
                    return null;
                }

                var discriminatorColumn = selectExpression.GetEntityProjectionExpression(entityType, sharingTypes)
                    .BindProperty(concreteEntityType.GetDiscriminatorProperty());

                return Equal(discriminatorColumn, Constant(concreteEntityType.GetDiscriminatorValue()));

            }
            else
            {
                var discriminatorColumn = selectExpression.GetEntityProjectionExpression(entityType, sharingTypes)
                    .BindProperty(concreteEntityTypes[0].GetDiscriminatorProperty());

                return In(discriminatorColumn, Constant(concreteEntityTypes.Select(et => et.GetDiscriminatorValue()).ToList()), negated: false);
            }
        }

        private SqlExpression GenerateSharedTableExpression(
            SelectExpression selectExpression, IEntityType entityType, HashSet<IEntityType> sharingTypes)
        {
            if (sharingTypes.Count == 1
                || !entityType.FindForeignKeys(entityType.FindPrimaryKey().Properties)
                    .Any(fk => fk.PrincipalKey.IsPrimaryKey()
                               && fk.PrincipalEntityType != entityType
                               && sharingTypes.Contains(fk.PrincipalEntityType)))
            {
                return null;
            }

            SqlExpression predicate = null;
            var entityProjectionExpression = selectExpression.GetEntityProjectionExpression(entityType, sharingTypes);
            var requiredNonPkProperties = entityType.GetProperties().Where(p => !p.IsNullable && !p.IsPrimaryKey()).ToList();
            if (requiredNonPkProperties.Count > 0)
            {
                predicate = IsNotNull(requiredNonPkProperties[0], entityProjectionExpression);

                if (requiredNonPkProperties.Count > 1)
                {
                    predicate
                        = requiredNonPkProperties
                            .Skip(1)
                            .Aggregate(
                                predicate, (current, property) =>
                                    AndAlso(
                                        IsNotNull(property, entityProjectionExpression),
                                        current));
                }
            }
            else
            {
                var allNonPkProperties = entityType.GetProperties().Where(p => !p.IsPrimaryKey()).ToList();
                if (allNonPkProperties.Count > 0)
                {
                    predicate = IsNotNull(allNonPkProperties[0], entityProjectionExpression);

                    if (allNonPkProperties.Count > 1)
                    {
                        predicate
                            = allNonPkProperties
                                .Skip(1)
                                .Aggregate(
                                    predicate, (current, property) =>
                                        OrElse(
                                            IsNotNull(property, entityProjectionExpression),
                                            current));
                    }
                }
            }

            return predicate;
        }

        private SqlExpression IsNotNull(IProperty property, EntityProjectionExpression entityProjection)
            => IsNotNull(entityProjection.BindProperty(property));
    }
}
