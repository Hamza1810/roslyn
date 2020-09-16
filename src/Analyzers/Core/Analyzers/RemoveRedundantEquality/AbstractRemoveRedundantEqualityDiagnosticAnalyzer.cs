﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeAnalysis.RemoveRedundantEquality
{
    internal abstract class AbstractRemoveRedundantEqualityDiagnosticAnalyzer
        : AbstractBuiltInCodeStyleDiagnosticAnalyzer
    {
        private readonly ISyntaxFacts _syntaxFacts;

        protected AbstractRemoveRedundantEqualityDiagnosticAnalyzer(ISyntaxFacts syntaxFacts)
            : base(IDEDiagnosticIds.RemoveRedundantEqualityDiagnosticId,
                   option: null,
                   new LocalizableResourceString(nameof(AnalyzersResources.Remove_redundant_equality), AnalyzersResources.ResourceManager, typeof(AnalyzersResources)))
        {
            _syntaxFacts = syntaxFacts;
        }

        public override DiagnosticAnalyzerCategory GetAnalyzerCategory()
            => DiagnosticAnalyzerCategory.SemanticSpanAnalysis;

        protected override void InitializeWorker(AnalysisContext context)
            => context.RegisterOperationAction(AnalyzeBinaryOperator, OperationKind.BinaryOperator);

        private void AnalyzeBinaryOperator(OperationAnalysisContext context)
        {
            var operation = (IBinaryOperation)context.Operation;
            if (operation.OperatorMethod is not null)
            {
                // We shouldn't report diagnostic on overloaded operator as the behavior can change.
                return;
            }

            if (operation.OperatorKind is not (BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals))
            {
                return;
            }

            if (!_syntaxFacts.IsBinaryExpression(operation.Syntax))
            {
                return;
            }

            var rightOperand = operation.RightOperand;
            var leftOperand = operation.LeftOperand;
            if (rightOperand.Type.SpecialType != SpecialType.System_Boolean ||
                leftOperand.Type.SpecialType != SpecialType.System_Boolean)
            {
                return;
            }

            var isOperatorEquals = operation.OperatorKind == BinaryOperatorKind.Equals;
            _syntaxFacts.GetPartsOfBinaryExpression(operation.Syntax, out _, out var operatorToken, out _);

            if (TryGetLiteralValue(rightOperand) == isOperatorEquals ||
                TryGetLiteralValue(leftOperand) == isOperatorEquals)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptor,
                    operatorToken.GetLocation(),
                    additionalLocations: new[] { operation.Syntax.GetLocation() }));
            }

            return;

            static bool? TryGetLiteralValue(IOperation operand)
            {
                if (operand.ConstantValue.HasValue && operand.Kind == OperationKind.Literal &&
                    operand.ConstantValue.Value is bool constValue)
                {
                    return constValue;
                }
                return null;
            }
        }
    }
}
