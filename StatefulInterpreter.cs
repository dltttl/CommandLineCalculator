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
            var bufferStream = new MemoryStream();
            var serializer = new BinaryFormatter();
            var storageData = storage.Read();
            if (storageData.Length != 0)
            {
                var savedCommand = CommandModel.DeserializeFromStorage(storage);

                savedCommand.Storage = storage;
                savedCommand.UserConsole = userConsole;
                savedCommand.Culture = Culture;
                savedCommand._bufferStream = bufferStream;
                savedCommand._binaryFormatter = serializer;
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
                        var addCommand = new AddCommand(x, userConsole, Culture, storage, bufferStream, serializer);
                        addCommand.Run();
                        break;
                    case "median":
                        var medianCommand = new MedianCommand(x, userConsole, Culture, storage, bufferStream, serializer);
                        medianCommand.Run();
                        break;
                    case "help":
                        var helpCommand = new HelpCommand(x, userConsole, Culture, storage, bufferStream, serializer);
                        helpCommand.Run();
                        break;
                    case "rand":
                        var randCommand = new RandCommand(x, userConsole, Culture, storage, bufferStream, serializer);
                        randCommand.Run();
                        x = randCommand.X;
                        break;
                    default:
                        var notFoundCommand = new NotFoundCommand(x, userConsole, Culture, storage, bufferStream, serializer);
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

        public long X { get; protected set; }

        [NonSerialized] 
        public MemoryStream _bufferStream;
        [NonSerialized] 
        public BinaryFormatter _binaryFormatter;


        protected CommandModel(long x, Storage storage, UserConsole userConsole, CultureInfo culture, MemoryStream bufferStream,
            BinaryFormatter formatter)
        {
            _bufferStream = bufferStream;
            _binaryFormatter = formatter;
            Storage = storage;
            UserConsole = userConsole;
            Culture = culture;
            X = x;
        }
        public void SerializeAndWriteToStorage()
        {
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

        private readonly Queue<Action> _schedule;

        public RandCommand(long x, UserConsole userConsole, CultureInfo culture, Storage storage, MemoryStream bufferStream,
            BinaryFormatter formatter)
            : base(x, storage, userConsole, culture, bufferStream, formatter)
        {
            _schedule = new Queue<Action>();
            _schedule.Enqueue(Action.Read);
            SerializeAndWriteToStorage();
        }

        public override void Run()
        {
            while (_schedule.Count > 0)
            {
                switch (_schedule.Dequeue())
                {
                    case Action.Read:
                        var amountOfRands = int.Parse(UserConsole.ReadLine(), Culture);
                        for (var i = 0; i < amountOfRands; i++)
                        {
                            _schedule.Enqueue(Action.Write);
                        }

                        break;
                    case Action.Write:
                        UserConsole.WriteLine(X.ToString(Culture));
                        X = a * X % m;
                        break;
                }
                SerializeAndWriteToStorage();
            }
        }

    }

    [Serializable]
    internal sealed class AddCommand : CommandModel
    {
        private readonly List<int> _numbers = new List<int>(2);

        private Action _сurrCommand;

        private bool _isDone;


        public AddCommand(long x, UserConsole userConsole, CultureInfo culture, Storage storage,
            MemoryStream bufferStream,
            BinaryFormatter formatter)
            : base(x, storage, userConsole, culture, bufferStream, formatter)
        {
            _сurrCommand = Action.Read;
            SerializeAndWriteToStorage();
        }

        public override void Run()
        {
            while (!_isDone)
            {
                switch (_сurrCommand)
                {
                    case Action.Read:
                    {
                        _numbers.Add(int.Parse(UserConsole.ReadLine(), Culture));
                        _сurrCommand = _numbers.Count == 2 ? Action.Write : Action.Read;
                        break;
                    }
                    case Action.Write:
                    {
                        UserConsole.WriteLine(_numbers.Sum().ToString(Culture));
                        _isDone = true;
                        break;
                    }
                }
                SerializeAndWriteToStorage();
            }
        }
    }

    [Serializable]
    internal sealed class MedianCommand : CommandModel
    {
        private readonly List<int> _numbers = new List<int>();

        private readonly Queue<Action> _schedule;

        private bool _knowAmountOfNumbers;

        public MedianCommand(long x, UserConsole userConsole, CultureInfo culture, Storage storage, MemoryStream bufferStream,
            BinaryFormatter formatter)
            : base(x, storage, userConsole, culture, bufferStream, formatter)
        {
            _schedule = new Queue<Action>();
            _schedule.Enqueue(Action.Read);
            SerializeAndWriteToStorage();
        }

        public override void Run()
        {
            while (_schedule.Count>0)
            {
                switch (_schedule.Dequeue())
                {
                    case Action.Read:
                        if (!_knowAmountOfNumbers)
                        {
                            var count = int.Parse(UserConsole.ReadLine(), Culture);
                            for (var i = 0; i < count; i++)
                            {
                                _schedule.Enqueue(Action.Read);
                            }
                            _schedule.Enqueue(Action.Write);
                            _knowAmountOfNumbers = true;
                        }
                        else
                        {
                            _numbers.Add(int.Parse(UserConsole.ReadLine(), Culture));
                        }
                        SerializeAndWriteToStorage();
                        break;
                    case Action.Write:
                        UserConsole.WriteLine(CalculateMedian(_numbers).ToString(Culture));
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

        private readonly Queue<(Action action, string value)> _helpSchedule;

        public HelpCommand(long x, UserConsole userConsole, CultureInfo culture, Storage storage, MemoryStream bufferStream,
            BinaryFormatter formatter)
            : base(x, storage, userConsole, culture, bufferStream, formatter)
        {
            _helpSchedule = new Queue<(Action action, string value)>();
            _helpSchedule.Enqueue((Action.Write, "Укажите команду, для которой хотите посмотреть помощь"));
            _helpSchedule.Enqueue((Action.Write, commands));
            _helpSchedule.Enqueue((Action.Write, exitMessage));

            SerializeAndWriteToStorage();
        }

        public override void Run()
        {
            while (_helpSchedule.Count>0)
            {
                var (action, value) = _helpSchedule.Dequeue();
                switch (action)
                {
                    case Action.Write:
                        UserConsole.WriteLine(value);
                        if (value == exitMessage)
                        {
                            _helpSchedule.Enqueue((Action.Read, ""));
                        }
                        SerializeAndWriteToStorage();
                        break;

                    case Action.Read:
                        var inputCommandNameForHelp = UserConsole.ReadLine();

                        switch (inputCommandNameForHelp.Trim())
                        {
                            case "end":
                                break;
                            case "add":
                                _helpSchedule.Enqueue((Action.Write, "Вычисляет сумму двух чисел"));
                                _helpSchedule.Enqueue((Action.Write, exitMessage));
                                break;
                            case "median":
                                _helpSchedule.Enqueue((Action.Write, "Вычисляет медиану списка чисел"));
                                _helpSchedule.Enqueue((Action.Write, exitMessage));
                                break;
                            case "rand":
                                _helpSchedule.Enqueue((Action.Write, "Генерирует список случайных чисел"));
                                _helpSchedule.Enqueue((Action.Write, exitMessage));
                                break;
                            default:
                                _helpSchedule.Enqueue((Action.Write, "Такой команды нет"));
                                _helpSchedule.Enqueue((Action.Write, commands));
                                _helpSchedule.Enqueue((Action.Write, exitMessage));
                                break;
                        }

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

        public NotFoundCommand(long x, UserConsole userConsole, CultureInfo culture, Storage storage, MemoryStream bufferStream, 
            BinaryFormatter formatter)
            : base(x, storage, userConsole, culture, bufferStream, formatter)
        {
            SerializeAndWriteToStorage();
        }

        private bool _isDone;

        public override void Run()
        {
            if (!_isDone)
            {
                UserConsole.WriteLine(message);
                _isDone = true;
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