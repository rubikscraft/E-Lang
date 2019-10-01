using System;
using System.Linq;
using System.Linq.Expressions;
using Sprache;

namespace E_Lang.src
{
  public class EParser
  {
    // Base characters
    public static readonly Parser<string> Space = Parse.Text(Parse.WhiteSpace.AtLeastOnce());
    public static readonly Parser<char> Colon = Parse.Token(Parse.Char(':'));
    public static readonly Parser<char> Comma = Parse.Token(Parse.Char(','));
    public static readonly Parser<char> EndLine = Parse.Token(Parse.Char(';'));

    public static readonly Parser<char> BraceOpen = Parse.Token(Parse.Char('{'));
    public static readonly Parser<char> BraceClose = Parse.Token(Parse.Char('}'));
    public static readonly Parser<char> RoundBraceOpen = Parse.Token(Parse.Char('('));
    public static readonly Parser<char> RoundBraceClose = Parse.Token(Parse.Char(')'));
    public static readonly Parser<char> BracketOpen = Parse.Token(Parse.Char('['));
    public static readonly Parser<char> BracketClose = Parse.Token(Parse.Char(']'));

    public static readonly Parser<string> Word = Parse
      .LetterOrDigit
      .XOr(
        Parse.Char('-')
      ).XOr(
        Parse.Char('_')
      ).AtLeastOnce().Text();

    // Comment parser
    static readonly CommentParser Comment = new CommentParser("#", "###", "###", "\n");

    // Simple word parser
    static readonly Parser<EWord> EWord =
      from word in Word
      select new EWord { word = word };

    // Arrow parser
    static readonly Parser<EToken> ArrowLeft =
      from arrow in Parse.String("<-").Token().Text()
      select new EToken { token = arrow };
    static readonly Parser<EToken> ArrowRight =
      from arrow in Parse.String("->").Token().Text()
      select new EToken { token = arrow };

    // Parser for types that you assign and create
    static readonly Parser<EType> CreateableType =
      from typeName in Parse.String("int")
      /* .Or(Parse.String("boolean"))
      .Or(Parse.String("string"))
      .Or(Parse.String("void"))*/
      .Text()
      select new EType { type = typeName };
    // Parser for the other types
    static readonly Parser<EType> UsableType =
      from typeName in Parse.String("Function").Text()
      select new EType { type = typeName };
    // Parser for all types
    static readonly Parser<EType> Type = CreateableType.Or(UsableType);

    public static Parser<ESolvable> ESolvable = Parse.Ref(() => ESolvableParser.ESolvable);

    // Parse a subprogram a.k.a. a function
    static readonly Parser<EOperation[]> ESubProgram =
      from operations in Parse.Ref(() => EOperation).Many().Contained(BraceOpen, BraceClose)
      select operations.ToArray();

    static readonly Parser<EFunctionArgument> EFunctionArgument =
      from type in Type
      from colon in Colon
      from name in EWord
      select new EFunctionArgument { type = type, variable = name };

    static readonly Parser<EFunctionArgument[]> EFunctionArguments =
      from arguments in EFunctionArgument
        .DelimitedBy(Comma)
        .Optional()
        .Contained(RoundBraceOpen, RoundBraceClose)
      select arguments.IsDefined ? arguments.Get().ToArray() : new src.EFunctionArgument[] { };

    static readonly Parser<ESolvable[]> ECallArguments =
      from open in BraceOpen
      from solvable in
        ESolvable
        .DelimitedBy(Comma)
        .Optional()
      from close in BraceClose
      select solvable.IsDefined ? solvable.Get().ToArray() : new src.ESolvable[] { };


    // Parses a create variable operation
    static readonly Parser<ECreateOperation> ECreateOperation =
      from keyword in Parse.String("create")
      from space in Space
      from type in CreateableType
      from colon in Parse.Token(Parse.Char(':'))
      from name in EWord
      from semicolon in EndLine
      select new ECreateOperation { type = type, name = name };

    // Parses a assign variable operation
    static readonly Parser<EAssignOperation> EAssignOperation =
      from variable in EWord
      from arrow in ArrowLeft
      from content in ESolvable
      from semicolon in EndLine
      select new EAssignOperation { variable = variable, content = content };

    // Parses a check statement, comparable to an if statment without an else
    static readonly Parser<ECheckOperation> ECheckOperation =
      from keyword in Parse.String("check")
      from solvable in ESolvable.Contained(BraceOpen, BraceClose)
      from arrow in ArrowRight
      from operations in ESubProgram
      from semicolon in EndLine
      select new ECheckOperation { check = solvable, operations = operations };

    static readonly Parser<EFunctionOperation> EFunctionOperation =
      from keyword in Parse.String("function")
      from arguments in EFunctionArguments
      from rightarrow in ArrowRight
      from type in CreateableType
      from colon in Colon
      from name in EWord
      from leftarrow in ArrowLeft
      from operations in ESubProgram
      from semicolon in EndLine
      select new EFunctionOperation { name = name, type = type, arguments = arguments, operations = operations };

    public static readonly Parser<ECallOperation> ECallOperation =
      from arguments in ECallArguments
      from arrow in ArrowRight
      from word in EWord
      from assign in (
        from secondArrow in ArrowRight
        from variable in EWord
        select variable
      ).Optional()
      from semicolon in EndLine
      select assign.IsDefined ?
        new ECallOperation { callFunc = word, arguments = arguments, setVariable = assign.Get(), alsoSet = true } :
        new ECallOperation { callFunc = word, arguments = arguments };

    static readonly Parser<EExpressionOperation> EExpressionOperation =
      from solvable in ESolvable
      from semicolon in EndLine
      select new EExpressionOperation { expression = solvable };

    // Parses different types of operations
    static readonly Parser<EOperation> EOperation =
        ECreateOperation
         .Or<EOperation>(EAssignOperation)
         .Or(ECallOperation)
         .Or(ECheckOperation)
         .Or(EFunctionOperation)
         .Or(EExpressionOperation);

    static readonly Parser<EOperation> EComment =
      from comment in Comment.MultiLineComment.Or(Comment.SingleLineComment).Token()
      select new EOperation { };

    static readonly Parser<EOperation> EOperations =
      from comment in EComment.Many().Optional()
      from operation in EOperation
      select operation;

    // Parses the whole program
    public static Parser<EProgram> EProgram =
      from space in Parse.WhiteSpace.Many()
      from operations in EOperations.Many().End()
      select new EProgram { operations = operations.ToArray() };

    public static string Test()
    {
      //return ESolvable.End().Parse("4").ToString();
      //return EProgram.Parse("poop();").ToString();
      return ECheckOperation.End().Parse("check (itWorks + true) -> {};").ToString();
    }

  }

  class ESolvableParser
  {
    static Parser<ESOperator> Operator(string op, ExpressionType opType)
    {
      return Parse.String(op).Token().Return(new ESOperator { type = opType, op = op });
    }

    static readonly Parser<ESOperator> Add = Operator("+", ExpressionType.AddChecked);
    static readonly Parser<ESOperator> Subtract = Operator("-", ExpressionType.SubtractChecked);
    static readonly Parser<ESOperator> Multiply = Operator("*", ExpressionType.MultiplyChecked);
    static readonly Parser<ESOperator> Divide = Operator("/", ExpressionType.Divide);

    static readonly Parser<ESNumber> Number =
      (
        from dec in Parse.Decimal.Token()
        select new ESNumber(decimal.Parse(dec))
      ).Named("number");

    static readonly Parser<ESVariable> Variable =
      from word in EParser.Word
      select new ESVariable(word);

    static readonly Parser<ESExpression> Expression =
      (
        (
          from lparen in EParser.BraceOpen
          from expr in Parse.Ref(() => AddSubtract)
          from rparen in EParser.BraceClose
          select expr
        )
        .XOr(Number)
        .XOr(Variable)
      ).Token();


    static readonly Parser<ESExpression> MultiplyDivide = Parse.ChainOperator(Multiply.Or(Divide), Expression, ESExpression.CombineExpression);

    static readonly Parser<ESExpression> AddSubtract = Parse.ChainOperator(Add.Or(Subtract), MultiplyDivide, ESExpression.CombineExpression);

    public static Parser<ESolvable> ESolvable = AddSubtract.Select(solvable => new ESolvable { expression = solvable });

    public static readonly Parser<ESExpression> Lambda =
        AddSubtract.End();

  }
}
