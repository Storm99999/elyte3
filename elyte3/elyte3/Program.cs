using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace TextEditor
{

    class Program
    {
        private const uint STD_OUTPUT_HANDLE = 0xFFFFFFF5; // SOD, standard output device ? got this off msdocs

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(uint nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleActiveScreenBuffer(IntPtr hConsoleOutput);

        static void Main(string[] args)
        {
            IntPtr stdOutputHandle = GetStdHandle(STD_OUTPUT_HANDLE);
            if (stdOutputHandle != IntPtr.Zero)
            {
                if (SetConsoleActiveScreenBuffer(stdOutputHandle))
                {
                    Console.WriteLine("[elyte3] Switched to the active screen buffer.");
                }
                else
                {
                    Console.WriteLine("[elyte3] Failed to switch to the active screen buffer. Error code: " + Marshal.GetLastWin32Error());
                }
            }
            else
            {
                Console.WriteLine("[elyte3] Failed to get the standard output handle. Error code: " + Marshal.GetLastWin32Error());
            }
            Console.WriteLine("[elyte3] Welcome to Elyte3!");
            if (args.Length >= 0 && args[0].Length > 0)
            {
                if (File.Exists(args[0]))
                {
                    Console.WriteLine("[elyte3] File detected, opening!");
                }
                else
                {
                    Console.WriteLine("[elyte3] Provided file does not exist.");
                    Console.ReadLine();
                    return;
                }
            }
            else
            {
                Console.WriteLine("[elyte3] Elyte has to be used either with a custom distro, or manual file opening with args: \n elyte3.exe <FILE PATH>");
                Console.ReadLine();
                return;
            }
            Thread.Sleep(1000);
            new Editor(args[0]).Run();
        }
    }

    class Editor
    {
        Buffer _buffer;
        Cursor _cursor;
        Stack<object> _history;

        public Editor(string filepath)
        {
            var lines = File.ReadAllLines(filepath)
                            .Where(x => x != Environment.NewLine);

            _buffer = new Buffer(lines);
            _cursor = new Cursor();
            _history = new Stack<object>();
        }

        public void Run()
        {
            while (true)
            {
                Render();
                HandleInput();
            }
        }

        private void HandleInput()
        {
            var key = Console.ReadKey(true); // ReadKey without displaying the pressed key funny!

            switch (key.Key)
            {
                case ConsoleKey.Q when (key.Modifiers & ConsoleModifiers.Control) != 0:
                    Environment.Exit(0);
                    break;

                case ConsoleKey.P when (key.Modifiers & ConsoleModifiers.Control) != 0:
                    _cursor = _cursor.Up(_buffer);
                    break;

                case ConsoleKey.N when (key.Modifiers & ConsoleModifiers.Control) != 0:
                    _cursor = _cursor.Down(_buffer);
                    break;

                case ConsoleKey.B when (key.Modifiers & ConsoleModifiers.Control) != 0:
                    _cursor = _cursor.Left(_buffer);
                    break;

                case ConsoleKey.Z when (key.Modifiers & ConsoleModifiers.Control) != 0:
                    _cursor = _cursor.Right(_buffer);
                    break;

                case ConsoleKey.U when (key.Modifiers & ConsoleModifiers.Control) != 0:
                    RestoreSnapshot();
                    break;

                case ConsoleKey.Backspace:
                    if (_cursor.Col > 0)
                    {
                        SaveSnapshot();
                        _buffer = _buffer.Delete(_cursor.Row, _cursor.Col - 1);
                        _cursor = _cursor.Left(_buffer);
                    }
                    break;

                case ConsoleKey.Enter:
                    SaveSnapshot();
                    _buffer = _buffer.SplitLine(_cursor.Row, _cursor.Col);
                    _cursor = _cursor.Down(_buffer).MoveToCol(0);
                    break;

                case ConsoleKey.LeftArrow:
                    _cursor = _cursor.Left(_buffer);
                    break;

                case ConsoleKey.RightArrow:
                    _cursor = _cursor.Right(_buffer);
                    break;

                case ConsoleKey.UpArrow:
                    _cursor = _cursor.Up(_buffer);
                    break;

                case ConsoleKey.Tab:
                    SaveSnapshot();
                    int spacesToAdd = 4;
                    string spaces = new string(' ', spacesToAdd);
                    _buffer = _buffer.Insert(spaces, _cursor.Row, _cursor.Col);
                    _cursor = _cursor.Right(_buffer).MoveToCol(_cursor.Col + spacesToAdd);
                    break;

                case ConsoleKey.DownArrow:
                    _cursor = _cursor.Down(_buffer);
                    break;

                default:
                    bool IsTextChar(ConsoleKeyInfo character)
                    {
                        return !char.IsControl(character.KeyChar);
                    }

                    if (IsTextChar(key))
                    {
                        SaveSnapshot();
                        _buffer = _buffer.Insert(key.KeyChar.ToString(), _cursor.Row, _cursor.Col);
                        _cursor = _cursor.Right(_buffer);
                    }
                    break;
            }


        }

        private void Render()
        {
            ANSI.ClearScreen();
            ANSI.MoveCursor(0, 0);
            _buffer.Render();
            ANSI.MoveCursor(_cursor.Row, _cursor.Col);
        }

        private void SaveSnapshot()
        {
            _history.Push(_cursor);
            _history.Push(_buffer);
        }

        private void RestoreSnapshot()
        {
            if (_history.Count > 0)
            {
                _buffer = (Buffer)_history.Pop();
                _cursor = (Cursor)_history.Pop();
            }
        }
    }

    class Buffer
    {
        string[] _lines;

        public Buffer(IEnumerable<string> lines)
        {
            _lines = lines.ToArray();
        }

        public void Render()
        {
            for (int i = 0; i < _lines.Length; i++)
            {
                string line = _lines[i];
                RenderLineWithColor(line);
            }
        }

        private void RenderLineWithColor(string line)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            int currentIndex = 0;

            while (currentIndex < line.Length)
            {
                if (currentIndex + 1 < line.Length && line.Substring(currentIndex, 2) == "//")
                {
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.Write(line.Substring(currentIndex));
                    Console.ResetColor();
                    currentIndex = line.Length; // Move to the end of the line yes!!!
                }
                // Check for comments starting with "/*"
                else if (currentIndex + 1 < line.Length && line.Substring(currentIndex, 2) == "/*")
                {
                    int endCommentIndex = line.IndexOf("*/", currentIndex + 2);

                    if (endCommentIndex != -1)
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.Write(line.Substring(currentIndex, endCommentIndex - currentIndex + 4));
                        Console.ResetColor();
                        currentIndex = endCommentIndex + 4;
                    }
                    else
                    {
                        // Unmatched comment just continue with default
                        Console.ForegroundColor = originalColor;
                        Console.Write(line.Substring(currentIndex));
                        Console.ResetColor();
                        currentIndex = line.Length;
                    }
                }
                // string check lulz
                else if (line[currentIndex] == '\"')
                {
                    int endQuoteIndex = line.IndexOf('\"', currentIndex + 1);

                    if (endQuoteIndex != -1)
                    {
                        // highlight it
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(line.Substring(currentIndex, endQuoteIndex - currentIndex + 1));
                        Console.ResetColor();
                        currentIndex = endQuoteIndex + 1;
                    }
                    else
                    {
                        // e
                        Console.ForegroundColor = originalColor;
                        Console.Write(line.Substring(currentIndex));
                        Console.ResetColor();
                        currentIndex = line.Length;
                    }
                }
                else
                {
                    int nextWhitespace = line.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '\"' }, currentIndex);

                    if (nextWhitespace == -1)
                    {
                        nextWhitespace = line.Length;
                    }

                    int length = nextWhitespace - currentIndex;
                    string word = line.Substring(currentIndex, length);

                    Console.ForegroundColor = GetJavaScriptSyntaxColor(word.ToLowerInvariant(), originalColor);

                    if (currentIndex + length < line.Length)
                    {
                        Console.Write(word + line[currentIndex + length]); 
                    }
                    else
                    {
                        Console.Write(word);
                    }

                    Console.ResetColor();

                    currentIndex = nextWhitespace + 1;
                }
            }

            Console.WriteLine();
        }

        // This is awful, please rewrite this, whoever can.
        private ConsoleColor GetJavaScriptSyntaxColor(string word, ConsoleColor defaultColor)
        {
            switch (word)
            {
                case "function":
                    return ConsoleColor.Magenta;
                case "if":
                    return ConsoleColor.Magenta;
                case "else":
                    return ConsoleColor.Magenta;
                case "while":
                    return ConsoleColor.Magenta;
                case "for":
                    return ConsoleColor.Magenta;
                case "var":
                    return ConsoleColor.Magenta;
                case "let":
                    return ConsoleColor.Magenta;
                case "const":
                    return ConsoleColor.Magenta;
                case "return":
                    return ConsoleColor.Magenta;
                case "console":
                    return ConsoleColor.Blue;

                case "true":
                    return ConsoleColor.Cyan;
                case "false":
                    return ConsoleColor.Cyan;

                case "null":
                    return ConsoleColor.Cyan;

                case "//":
                    return ConsoleColor.Gray;
                case "/*":
                    return ConsoleColor.Gray;
                case "*/":
                    return ConsoleColor.Gray;


                default:
                    return defaultColor;
            }
        }


        public int LineCount()
        {
            return _lines.Count();
        }

        public int LineLength(int row)
        {
            return _lines[row].Length;
        }

        internal Buffer Insert(string character, int row, int col)
        {
            var linesDeepCopy = _lines.Select(x => x).ToArray();
            linesDeepCopy[row] = linesDeepCopy[row].Insert(col, character);
            return new Buffer(linesDeepCopy);
        }

        internal Buffer Delete(int row, int col)
        {
            var linesDeepCopy = _lines.Select(x => x).ToArray();
            linesDeepCopy[row] = linesDeepCopy[row].Remove(col, 1);
            return new Buffer(linesDeepCopy);
        }

        internal Buffer SplitLine(int row, int col)
        {
            var linesDeepCopy = _lines.Select(x => x).ToList();

            var line = linesDeepCopy[row];

            var newLines = new[] { line.Substring(0, col), line.Substring(col, line.Length - line.Substring(0, col).Length) };

            linesDeepCopy[row] = newLines[0];
            linesDeepCopy.Insert(row + 1, newLines[1]);


            return new Buffer(linesDeepCopy);
        }
    }

    class Cursor
    {
        public int Row { get; set; }
        public int Col { get; set; }


        public Cursor(int row = 0, int col = 0)
        {
            Row = row;
            Col = col;
        }

        internal Cursor Up(Buffer buffer)
        {
            return new Cursor(Row - 1, Col).Clamp(buffer);
        }

        internal Cursor Down(Buffer buffer)
        {
            return new Cursor(Row + 1, Col).Clamp(buffer);
        }


        internal Cursor Left(Buffer buffer)
        {
            return new Cursor(Row, Col - 1).Clamp(buffer);
        }

        internal Cursor Right(Buffer buffer)
        {
            return new Cursor(Row, Col + 1).Clamp(buffer);
        }

        private Cursor Clamp(Buffer buffer)
        {
            Row = Math.Min(buffer.LineCount() - 1, Math.Max(Row, 0));
            Col = Math.Min(buffer.LineLength(Row), Math.Max(Col, 0));
            return new Cursor(Row, Col);
        }

        internal Cursor MoveToCol(int col)
        {
            return new Cursor(Row, col);
        }
    }

    class ANSI
    {
        public static void ClearScreen()
        {
            Console.Clear();
        }

        public static void MoveCursor(int row, int col)
        {
            Console.CursorTop = row;
            Console.CursorLeft = col;
        }
    }
}