﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    internal abstract partial class SymbolKey
    {
        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        private class PropertySymbolKey : AbstractSymbolKey<PropertySymbolKey>
        {
            [JsonProperty] private readonly SymbolKey _containerKey;
            [JsonProperty] private readonly string _metadataName;
            [JsonProperty] private readonly RefKind[] _refKinds;
            [JsonProperty] private readonly SymbolKey[] _originalParameterTypeKeys;
            [JsonProperty] private readonly bool _isIndexer;

            public PropertySymbolKey(
                SymbolKey _containerKey, string _metadataName, RefKind[] _refKinds, 
                SymbolKey[] _originalParameterTypeKeys, bool _isIndexer)
            {
                this._containerKey = _containerKey;
                this._metadataName = _metadataName;
                this._refKinds = _refKinds;
                this._originalParameterTypeKeys = _originalParameterTypeKeys;
                this._isIndexer = _isIndexer;
            }

            internal PropertySymbolKey(IPropertySymbol symbol, Visitor visitor)
            {
                _containerKey = GetOrCreate(symbol.ContainingSymbol, visitor);
                _metadataName = symbol.MetadataName;
                _isIndexer = symbol.IsIndexer;
                _refKinds = symbol.Parameters.Select(p => p.RefKind).ToArray();
                _originalParameterTypeKeys = symbol.OriginalDefinition.Parameters.Select(p => GetOrCreate(p.Type, visitor)).ToArray();
            }

            public override SymbolKeyResolution Resolve(Compilation compilation, bool ignoreAssemblyKey, CancellationToken cancellationToken)
            {
                var container = _containerKey.Resolve(compilation, ignoreAssemblyKey, cancellationToken);
                var namedTypes = GetAllSymbols<INamedTypeSymbol>(container);
                var properties = namedTypes
                    .SelectMany(t => t.GetMembers())
                    .OfType<IPropertySymbol>()
                    .Where(p => p.Parameters.Length == _refKinds.Length && p.MetadataName == _metadataName && p.IsIndexer == _isIndexer);

                var comparisonOptions = new ComparisonOptions(compilation.IsCaseSensitive, ignoreAssemblyKey, compareMethodTypeParametersByName: true);
                var matchingProperties = properties.Where(p =>
                    ParametersMatch(comparisonOptions, p.OriginalDefinition.Parameters, _refKinds, _originalParameterTypeKeys, cancellationToken));

                return CreateSymbolInfo(matchingProperties);
            }

            internal override bool Equals(PropertySymbolKey other, ComparisonOptions options)
            {
                var comparer = SymbolKeyComparer.GetComparer(options.IgnoreCase, options.IgnoreAssemblyKey, compareMethodTypeParametersByName: true);
                return
                    other._isIndexer == _isIndexer &&
                    other._refKinds.Length == _refKinds.Length &&
                    Equals(options.IgnoreCase, other._metadataName, _metadataName) &&
                    other._containerKey.Equals(_containerKey, options) &&
                    other._refKinds.SequenceEqual(_refKinds) &&
                    SequenceEquals(other._originalParameterTypeKeys, _originalParameterTypeKeys, comparer);
            }

            internal override int GetHashCode(ComparisonOptions options)
            {
                // TODO(cyrusn): Consider hashing the parameters as well
                return
                    Hash.Combine(_isIndexer,
                    Hash.Combine(_refKinds.Length,
                    Hash.Combine(GetHashCode(options.IgnoreCase, _metadataName),
                                 _containerKey.GetHashCode(options))));
            }
        }
    }
}