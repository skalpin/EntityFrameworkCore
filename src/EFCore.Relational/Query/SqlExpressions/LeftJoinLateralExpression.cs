﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public class LeftJoinLateralExpression : JoinExpressionBase
    {
        public LeftJoinLateralExpression(TableExpressionBase table)
            : base(table)
        {
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
            => Update((TableExpressionBase)visitor.Visit(Table));

        public virtual LeftJoinLateralExpression Update(TableExpressionBase table)
            => table != Table
                ? new LeftJoinLateralExpression(table)
                : this;

        public override void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("LEFT JOIN LATERAL ");
            expressionPrinter.Visit(Table);
        }

        public override bool Equals(object obj)
            => obj != null
            && (ReferenceEquals(this, obj)
                || obj is LeftJoinLateralExpression leftJoinLateralExpression
                    && Equals(leftJoinLateralExpression));

        private bool Equals(LeftJoinLateralExpression leftJoinLateralExpression)
            => base.Equals(leftJoinLateralExpression);

        public override int GetHashCode() => base.GetHashCode();
    }
}
