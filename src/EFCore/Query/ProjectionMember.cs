// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore.Query
{
    [DebuggerDisplay("{ToString(), nq}")]
    public class ProjectionMember
    {
        private readonly IList<MemberInfo> _memberChain;

        public ProjectionMember()
        {
            _memberChain = new List<MemberInfo>();
        }

        public ProjectionMember(IEntityType root)
            : this()
        {
            Root = root;
        }

        private ProjectionMember(IList<MemberInfo> memberChain)
        {
            _memberChain = memberChain;
        }

        private ProjectionMember(IEntityType root, IList<MemberInfo> memberChain)
            : this(memberChain)
        {
            Root = root;
        }

        public virtual ProjectionMember Append(MemberInfo member)
        {
            var existingChain = _memberChain.ToList();
            existingChain.Add(member);

            return new ProjectionMember(Root, existingChain);
        }

        public virtual ProjectionMember Prepend(MemberInfo member)
        {
            var existingChain = _memberChain.ToList();
            existingChain.Insert(0, member);

            return new ProjectionMember(Root, existingChain);
        }

        public virtual MemberInfo Last => _memberChain.LastOrDefault();

        public virtual IEntityType Root { get; set; }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            if (Root != null)
            {
                hash.Add(Root);
            }

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < _memberChain.Count; i++)
            {
                hash.Add(_memberChain[i]);
            }

            return hash.ToHashCode();
        }

        public override bool Equals(object obj)
            => obj != null
               && (obj is ProjectionMember projectionMember
                   && Equals(projectionMember));

        private bool Equals(ProjectionMember other)
        {
            if (Root != other.Root)
            {
                return false;
            }

            if (_memberChain.Count != other._memberChain.Count)
            {
                return false;
            }

            for (var i = 0; i < _memberChain.Count; i++)
            {
                if (!Equals(_memberChain[i], other._memberChain[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override string ToString()
            => _memberChain.Any() || Root != null
                ? string.Join(".",
                    (Root == null ? Enumerable.Empty<string>() : new List<string>() { Root.DisplayName() })
                    .Concat(_memberChain.Select(mi => mi.Name)))
                : "EmptyProjectionMember";
    }
}
