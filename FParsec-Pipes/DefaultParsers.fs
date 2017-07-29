﻿// The MIT License (MIT)
// Copyright (c) 2016 Robert Peele
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
// BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

[<AutoOpen>]
module FParsec.Pipes.DefaultParsers
open FParsec

/// Parse a character case insensitively. Returns the parsed character.
let pcharCI c : Parser<char, 'u> =
    let cfs = Text.FoldCase(c : char)
    fun stream ->
        if stream.SkipCaseFolded(cfs) then
             Reply(stream.Peek(-1))
        else Reply(Error, expectedString (string c))

/// Represents a parser whose output is ignored within a pipeline.
type IgnoreParser<'a, 'u> =
    | IgnoreParser of Parser<'a, 'u>
    static member (---) (pipe, IgnoreParser (p : Parser<'a, 'u>)) = appendIgnore pipe p
    static member (?--) (pipe, IgnoreParser (p : Parser<'a, 'u>)) = appendIgnoreBacktrackLeft pipe p
    static member (--?) (pipe, IgnoreParser (p : Parser<'a, 'u>)) = appendIgnoreBacktrackRight pipe p

/// Represents a parser whose output is captured within a pipeline.
type CaptureParser<'a, 'u> =
    | CaptureParser of Parser<'a, 'u>
    static member (---) (pipe, CaptureParser (p : Parser<'a, 'u>)) = appendCapture pipe p
    static member (?--) (pipe, CaptureParser (p : Parser<'a, 'u>)) = appendCaptureBacktrackLeft pipe p
    static member (--?) (pipe, CaptureParser (p : Parser<'a, 'u>)) = appendCaptureBacktrackRight pipe p

[<AllowNullLiteral>]
type DefaultParserOf<'a>() =
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<char>) = anyChar |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<float>) = pfloat |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<int8>) = pint8 |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<int16>) = pint16 |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<int32>) = pint32 |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<int64>) = pint64 |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<uint8>) = puint8 |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<uint16>) = puint16 |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<uint32>) = puint32 |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<uint64>) = puint64 |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf<Position>) = getPosition |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, _ : DefaultParserOf< ^x >) =
        (^x : (static member get_DefaultParser : unit -> Parser< ^x, unit >)()) |> IgnoreParser
and DefaultParser =
    | DefaultParser
    static member inline (%!!~~%) (_ : DefaultParser, cap : CaptureParser<_, _>) = cap
    static member inline (%!!~~%) (_ : DefaultParser, existing : Parser<'a, 'u>) = existing |> IgnoreParser

    static member inline (%!!~~%) (_ : DefaultParser, literal : char) = pchar literal |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, literal : string) = pstring literal |> IgnoreParser

    static member inline (%!!~~%) (_ : DefaultParser, list : _ list) =
        [| for parserish in list ->
            let (IgnoreParser parser) = DefaultParser %!!~~% parserish
            parser
        |] |> choice |> IgnoreParser
and CaseInsensitive<'a> =
    | CaseInsensitive of 'a
    static member inline (%!!~~%) (_ : DefaultParser, CaseInsensitive (literal : char)) = pcharCI literal |> IgnoreParser
    static member inline (%!!~~%) (_ : DefaultParser, CaseInsensitive (literal : string)) = pstringCI literal |> IgnoreParser

/// Mark `x` as being case insensitive.
/// Useful for use with `%`. For example `%ci "test"` is equivalent
/// to `pstringCI "test"`, while `%"test"` is equivalent to `pstring "test"`.
let ci x = CaseInsensitive x

/// Represents the default parser for the given type.
/// If the type `'a` has a default parser implemented, `p<'a>`
/// can be converted to a `Parser<'a, 'u>` with the % operator,
/// e.g. `%p<int>`.
let p<'a> = null : DefaultParserOf<'a>

/// Create a parser from `x`, if there is a single sensible parser possible.
/// For example, `defaultParser "str"` is equivalent to `pstring str`.
let inline defaultParser x =
    let (IgnoreParser parser) = DefaultParser %!!~~% x
    parser

/// Converts its argument to a parser via `defaultParser` and
/// marks the result as a captured input, which can be consumed
/// by the function at the end of a pipe.
let inline (~+.) x = CaptureParser (defaultParser x)

/// Chains `parser` onto `pipe`.
/// `parser` will be converted to a parser and may be captured or ignored based
/// on whether `+.` was used on it.
let inline (--) (pipe : Pipe<'inp, 'out, 'fn, 'r, 'u>) parser : Pipe<_, _, 'fn, _, 'u> =
    pipe --- (DefaultParser %!!~~% parser)

/// Chains `parser` onto `pipe`, with backtracking if `pipe` fails prior to `parser`.
/// `parser` will be converted to a parser and may be captured or ignored based
/// on whether `+.` was used on it.
let inline (?-) (pipe : Pipe<'inp, 'out, 'fn, 'r, 'u>) parser : Pipe<_, _, 'fn, _, 'u> =
    pipe ?-- (DefaultParser %!!~~% parser)

/// Chains `parser` onto `pipe`, with backtracking if `pipe` fails prior to `parser`
/// or `parser` fails without changing the parser state.
/// `parser` will be converted to a parser and may be captured or ignored based
/// on whether `+.` was used on it.
let inline (-?) (pipe : Pipe<'inp, 'out, 'fn, 'r, 'u>) parser : Pipe<_, _, 'fn, _, 'u> =
    pipe --? (DefaultParser %!!~~% parser)

/// Creates a pipe starting with `parser`. Shorthand for `pipe -- parser`.
let inline (~%%) parser : Pipe<_, _, _, _, _> =
    pipe -- parser

/// Prefix operator equivalent to `defaultParser x`.
let inline (~%) x = defaultParser x

/// Defines a self-referential parser given `defineParser`, which returns a parser given its own output parser.
/// The parser that will be passed to `defineParser` is a `createParserForwardedToRef()` pointed at a reference
/// that will be assigned to `defineParser`'s output.
let precursive defineParser =
    let p, pref = createParserForwardedToRef()
    pref := defineParser p
    p