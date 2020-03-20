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

                savedCommand.Storage = storage;
                savedCommand.UserConsole = userConsole;
                savedCommand.Culture = Culture;
                savedCommand.Run();

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

        [NonSerialized] public UserConsole UserConsole;

        [NonSerialized]
        public CultureInfo Culture;

        protected int _position;

        public abstract bool IsDone { get; }

        public long X { get; protected set; }

        [NonSerialized]
        private MemoryStream _bufferStream = new MemoryStream();
        [NonSerialized]
        private BinaryFormatter _binaryFormatter = new BinaryFormatter();


        protected CommandModel(long x, Storage storage, UserConsole userConsole, CultureInfo culture)
        {
            Storage = storage;
            UserConsole = userConsole;
            Culture = culture;
            X = x;
        }
        public void SerializeAndWriteToStorage()
        {
            if (_bufferStream==null) _bufferStream = new MemoryStream();
            if (_binaryFormatter==null) _binaryFormatter = new BinaryFormatter();
            _binaryFormatter.Serialize(_bufferStream, this);
            Storage.Write(_bufferStream.ToArray());
            _bufferStream.Position = 0;
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

        private int randAmount;

        public override bool IsDone => _position > 0 && _position == randAmount + 1;

        public RandCommand(long x, UserConsole userConsole, CultureInfo culture, Storage storage)
            : base(x, storage, userConsole, culture)
        {
            SerializeAndWriteToStorage();
        }

        public override void Run()
        {
            if (_position == 0)
            {
                randAmount = int.Parse(UserConsole.ReadLine(), Culture);
                _position++;
                SerializeAndWriteToStorage();
            }
            for (var i = _position; i < randAmount+1; i++)
            {
                UserConsole.WriteLine(X.ToString(Culture));
                X = a * X % m;
                _position++;
                SerializeAndWriteToStorage();
            }
        }

    }

    [Serializable]
    internal sealed class AddCommand : CommandModel
    {
        private List<int> _variablesToAdd = new List<int>(2);

        public override bool IsDone => _position == 3;

        public AddCommand(long x, UserConsole userConsole, CultureInfo culture, Storage storage)
            : base(x, storage, userConsole, culture)
        {
            SerializeAndWriteToStorage();
        }

        public override void Run()
        {
            while (!IsDone)
            {
                if (_position <= 1)
                {
                    _variablesToAdd.Add(int.Parse(UserConsole.ReadLine(), Culture));
                }
                else
                {
                    UserConsole.WriteLine(_variablesToAdd.Sum().ToString(Culture));
                }

                _position++;
                SerializeAndWriteToStorage();
            }
        }
    }

    [Serializable]
    internal sealed class MedianCommand : CommandModel
    {
        private int _count;

        private List<int> _numbers = new List<int>();

        public override bool IsDone => _position == _schedule.Count;

        private List<Action> _schedule;

        public MedianCommand(long x, UserConsole userConsole, CultureInfo culture, Storage storage)
            : base(x, storage, userConsole, culture)
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
                            _count = int.Parse(UserConsole.ReadLine(), Culture);
                            _schedule.AddRange(Enumerable.Repeat(Action.Read, _count));
                            _schedule.Add(Action.Write);
                        }
                        else
                        {
                            _numbers.Add(int.Parse(UserConsole.ReadLine(), Culture));
                        }
                        _position++;
                        SerializeAndWriteToStorage();
                        break;
                    case Action.Write:
                        UserConsole.WriteLine(CalculateMedian(_numbers).ToString(Culture));
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
        [NonSerialized]
        private const string exitMessage = "Чтобы выйти из режима помощи введите end";
        private const string commands = "Доступные команды: add, median, rand";

        private string _inputCommandNameForHelp;

        private List<(Action action, string value)> _helpSchedule;

        public override bool IsDone => _position == _helpSchedule.Count;

        public HelpCommand(long x, UserConsole userConsole, CultureInfo culture, Storage storage)
            : base(x, storage, userConsole, culture)
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
                        UserConsole.WriteLine(_helpSchedule[i].value);
                        if (_helpSchedule[i].value == exitMessage)
                        {
                            _helpSchedule.Add((Action.Read, ""));
                        }
                        _position++;
                        SerializeAndWriteToStorage();
                        break;

                    case Action.Read:
                        _inputCommandNameForHelp = UserConsole.ReadLine();

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

        public NotFoundCommand(long x, UserConsole userConsole, CultureInfo culture, Storage storage)
            : base(x, storage, userConsole, culture)
        {
            SerializeAndWriteToStorage();
        }

        public override bool IsDone => _position == 1;

        public override void Run()
        {
            if (_position == 0)
            {
                UserConsole.WriteLine(message);
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