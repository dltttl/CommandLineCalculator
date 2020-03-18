using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace CommandLineCalculator
{
    public sealed class StatefulInterpreter : Interpreter
    {
        private static CultureInfo Culture => CultureInfo.InvariantCulture;

        public override void Run(UserConsole userConsole, Storage storage)
        {
            var x = 420L;
            var storageData = storage.Read();
            if (storageData.Length != 0)
            {
                var savedCommand = CommandModel.DeserializeFromStorage(storage);
                
                if (!savedCommand.IsDone)
                {
                    savedCommand.Storage = storage;
                    savedCommand.Console = userConsole;
                    savedCommand.Run();
                }
                x = savedCommand.X;

            }
            
            while (true)
            {
                var input = userConsole.ReadLine();
                switch (input.Trim())
                {
                    case "exit":
                        storage.Write(Array.Empty<byte>());
                        return;
                    case "add":
                        var addCommand = new AddCommand(x, userConsole, Culture, storage);
                        addCommand.Run();
                        break;
                    case "median":
                        var medianCommand = new MedianCommand(x, userConsole, Culture, storage);
                        medianCommand.Run();
                        break;
                    case "help":
                        var helpCommand = new HelpCommand(x, userConsole, Culture, storage);
                        helpCommand.Run();
                        break;
                    case "rand":
                        var randCommand = new RandCommand(x, userConsole, Culture, storage);
                        randCommand.Run();
                        x = randCommand.X;
                        break;
                    default:
                        var notFoundCommand = new NotFoundCommand(x, userConsole, Culture, storage);
                        notFoundCommand.Run();
                        break;
                }
            }
        }
    }

    [Serializable]
    internal abstract class CommandModel
    {
        [NonSerialized] public Storage Storage;

        [NonSerialized] public UserConsole Console;

        public CultureInfo Culture { get; set; }

        protected int _position;

        protected List<Action> _schedule;

        public long X { get; internal set; }

        public virtual bool IsDone => _position == _schedule.Count;

        protected CommandModel(long x, Storage storage, UserConsole console, CultureInfo culture)
        {
            Storage = storage;
            Console = console;
            Culture = culture;
            X = x;
        }
        public void SerializeAndWriteToStorage()
        {
            var bufferStream = new MemoryStream();
            var formatter = new BinaryFormatter();
            formatter.Serialize(bufferStream, this);
            Storage.Write(bufferStream.ToArray());
        }

        public static CommandModel DeserializeFromStorage(Storage storage)
        {
            var bufferStream = new MemoryStream(storage.Read());
            var formatter = new BinaryFormatter();
            return formatter.Deserialize(bufferStream) as CommandModel;
        }

        public abstract void Run();
    }

    [Serializable]
    internal sealed class RandCommand : CommandModel
    {
        private const int a = 16807;
        private const int m = 2147483647;
        

        public RandCommand(long x, UserConsole console, CultureInfo culture, Storage storage)
            : base(x, storage, console, culture)
        {
            _schedule = new List<Action> { Action.Read };
            SerializeAndWriteToStorage();
        }

        public override void Run()
        {
            for (var i = _position; i < _schedule.Count; i++)
            {
                var currentTask = _schedule[i];
                switch (currentTask)
                {
                    case Action.Read:
                    {
                        var count = int.Parse(Console.ReadLine(), Culture);
                        for (var j = 0; j < count; i++)
                        {
                            _schedule.Add(Action.Write);
                        }
                        _position++;
                        SerializeAndWriteToStorage();
                        break;
                    }
                    case Action.Write:
                    {
                        _position++;
                        Console.WriteLine(X.ToString(Culture));
                        X = a * X % m;
                        SerializeAndWriteToStorage();
                        break;
                    }
                }
            }
        }

    }

    [Serializable]
    internal sealed class AddCommand : CommandModel
    {
        private List<int> _variablesToAdd = new List<int>(2);

        public AddCommand(long x, UserConsole console, CultureInfo culture, Storage storage)
            : base(x, storage, console, culture)
        {
            _schedule = new List<Action>{Action.Read,Action.Read, Action.Write};
            SerializeAndWriteToStorage();
        }

        public override void Run()
        {
            for (var i = _position; i < _schedule.Count; i++)
            {
                switch (_schedule[i])
                {
                    case Action.Read:
                    {
                        _variablesToAdd.Add(int.Parse(Console.ReadLine(), Culture));
                        _position++;
                        SerializeAndWriteToStorage();
                        break;
                    }

                    case Action.Write:
                    {
                        Console.WriteLine((_variablesToAdd.Sum().ToString(Culture)));
                        _position++;
                        SerializeAndWriteToStorage();
                        break;
                    }
                }
            }
        }
    }

    [Serializable]
    internal sealed class MedianCommand : CommandModel
    {
        private int _count;

        private List<int> _numbers = new List<int>();

        public MedianCommand(long x, UserConsole console, CultureInfo culture, Storage storage)
            : base(x, storage, console, culture)
        {
            _schedule = new List<Action> { Action.Read };
            SerializeAndWriteToStorage();
        }

        public override void Run()
        {
            for (var i = _position; i < _schedule.Count; i++)
            {
                switch (_schedule[i])
                {
                    case Action.Read:
                        if (i == 0)
                        {
                            _count = int.Parse(Console.ReadLine(), Culture);
                            _schedule.AddRange(Enumerable.Repeat(Action.Read, _count));
                            _schedule.Add(Action.Write);
                        }
                        else
                        {
                            _numbers.Add(int.Parse(Console.ReadLine(), Culture));
                        }
                        _position++;
                        SerializeAndWriteToStorage();
                        break;
                    case Action.Write:
                        Console.WriteLine(CalculateMedian(_numbers).ToString(Culture));
                        _position++;
                        SerializeAndWriteToStorage();
                        break;
                }
            }
        }

        private double CalculateMedian(List<int> numbers)
        {
            numbers.Sort();
            var count = numbers.Count;
            if (count == 0)
                return 0;

            if (count % 2 == 1)
                return numbers[count / 2];

            return (numbers[count / 2 - 1] + numbers[count / 2]) / 2.0;
        }
    }

    [Serializable]
    internal sealed class HelpCommand : CommandModel
    {
        private const string exitMessage = "Чтобы выйти из режима помощи введите end";
        private const string commands = "Доступные команды: add, median, rand";

        private string _inputCommandNameForHelp;

        private List<(Action action, string value)>_helpSchedule;

        public override bool IsDone => _position == _helpSchedule.Count;
        public HelpCommand(long x, UserConsole console, CultureInfo culture, Storage storage)
            : base(x, storage, console, culture)
        {
            _helpSchedule = new List<(Action action, string value)>
            {
                (Action.Write, "Укажите команду, для которой хотите посмотреть помощь"),
                (Action.Write, commands),
                (Action.Write, exitMessage)
            };

            SerializeAndWriteToStorage();
        }

        public override void Run()
        {
            for (var i = _position; i < _helpSchedule.Count; i++)
            {
                switch (_helpSchedule[i].action)
                {
                    case Action.Write:
                        Console.WriteLine(_helpSchedule[i].value);
                        if (_helpSchedule[i].value == exitMessage)
                        {
                            _helpSchedule.Add((Action.Read, ""));
                        }
                        _position++;
                        SerializeAndWriteToStorage();
                        break;

                    case Action.Read:
                        _inputCommandNameForHelp = Console.ReadLine();

                        switch (_inputCommandNameForHelp)
                        {
                            case "end":
                                break;
                            case "add":
                                _helpSchedule.Add((Action.Write, "Вычисляет сумму двух чисел"));
                                _helpSchedule.Add((Action.Write, exitMessage));
                                break;
                            case "median":
                                _helpSchedule.Add((Action.Write, "Вычисляет медиану списка чисел"));
                                _helpSchedule.Add((Action.Write, exitMessage));
                                break;
                            case "rand":
                                _helpSchedule.Add((Action.Write, "Генерирует список случайных чисел"));
                                _helpSchedule.Add((Action.Write, exitMessage));
                                break;
                            default:
                                _helpSchedule.Add((Action.Write, "Такой команды нет"));
                                _helpSchedule.Add((Action.Write, commands));
                                _helpSchedule.Add((Action.Write, exitMessage));
                                break;
                        }

                        _position++;
                        SerializeAndWriteToStorage();
                        break;
                }


            }
        }
    }

    [Serializable]
    internal sealed class NotFoundCommand : CommandModel
    {
        private const string message = "Такой команды нет, используйте help для списка команд";

        public NotFoundCommand(long x, UserConsole console, CultureInfo culture, Storage storage)
            : base(x, storage, console, culture)
        {
            _schedule = new List<Action> { Action.Write};
            SerializeAndWriteToStorage();
        }

        public override void Run()
        {
            for (var i = _position; i < _schedule.Count; i++)
            {
                if (_schedule[i] != Action.Write) continue;
                Console.WriteLine(message);
                _position++;
                SerializeAndWriteToStorage();
            }
        }
    }


    internal enum Action : byte
    {
        Read,
        Write
    }

}