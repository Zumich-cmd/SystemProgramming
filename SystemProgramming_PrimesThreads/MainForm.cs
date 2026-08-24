using System.Diagnostics;

namespace SystemProgramming_PrimesThreads;

internal sealed class MainForm : Form
{
    private readonly TextBox _lowerBoundTextBox = new();
    private readonly TextBox _upperBoundTextBox = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly ListBox _primeNumbersListBox = new();
    private readonly Label _statusLabel = new();
    private readonly Label _countLabel = new();

    private Thread? _workerThread;
    private volatile bool _stopRequested;
    private long _primeCount;

    public MainForm()
    {
        Text = "Генератор простих чисел — потоки";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(720, 520);
        MinimumSize = new Size(650, 450);

        InitializeInterface();
        FormClosing += HandleFormClosing;
    }

    private void InitializeInterface()
    {
        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            Text = "Генератор простих чисел",
            Margin = new Padding(0, 0, 0, 4)
        };

        var descriptionLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            Text =
                "Порожня нижня межа означає 2. " +
                "Порожня верхня межа — генерацію до натискання «Зупинити»."
        };

        ConfigureTextBox(_lowerBoundTextBox, "Наприклад: 2");
        ConfigureTextBox(_upperBoundTextBox, "Необов'язково");

        _startButton.Text = "Запустити";
        _startButton.AutoSize = true;
        _startButton.Padding = new Padding(16, 5, 16, 5);
        _startButton.Click += StartGeneration;

        _stopButton.Text = "Зупинити";
        _stopButton.AutoSize = true;
        _stopButton.Padding = new Padding(16, 5, 16, 5);
        _stopButton.Enabled = false;
        _stopButton.Click += StopGeneration;

        _primeNumbersListBox.Dock = DockStyle.Fill;
        _primeNumbersListBox.Font = new Font("Consolas", 11);
        _primeNumbersListBox.IntegralHeight = false;
        _primeNumbersListBox.HorizontalScrollbar = true;

        _statusLabel.AutoSize = true;
        _statusLabel.Text = "Готово до запуску.";

        _countLabel.AutoSize = true;
        _countLabel.Text = "Знайдено: 0";
        _countLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);

        var boundsLayout = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 14, 0, 10)
        };

        boundsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        boundsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        boundsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        boundsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        boundsLayout.Controls.Add(
            new Label { AutoSize = true, Text = "Нижня межа:", Anchor = AnchorStyles.Left },
            0,
            0);
        boundsLayout.Controls.Add(_lowerBoundTextBox, 1, 0);
        boundsLayout.Controls.Add(
            new Label { AutoSize = true, Text = "Верхня межа:", Anchor = AnchorStyles.Left },
            2,
            0);
        boundsLayout.Controls.Add(_upperBoundTextBox, 3, 0);

        var buttonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 10)
        };
        buttonsPanel.Controls.Add(_startButton);
        buttonsPanel.Controls.Add(_stopButton);

        var resultHeader = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 6, 0, 4)
        };
        resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        resultHeader.Controls.Add(
            new Label
            {
                AutoSize = true,
                Text = "Прості числа",
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            },
            0,
            0);
        resultHeader.Controls.Add(_countLabel, 1, 0);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 7
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(descriptionLabel, 0, 1);
        root.Controls.Add(boundsLayout, 0, 2);
        root.Controls.Add(buttonsPanel, 0, 3);
        root.Controls.Add(resultHeader, 0, 4);
        root.Controls.Add(_primeNumbersListBox, 0, 5);
        root.Controls.Add(_statusLabel, 0, 6);

        Controls.Add(root);
        AcceptButton = _startButton;
    }

    private static void ConfigureTextBox(TextBox textBox, string placeholder)
    {
        textBox.Width = 180;
        textBox.PlaceholderText = placeholder;
        textBox.Margin = new Padding(8, 3, 24, 3);
        textBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
    }

    private void StartGeneration(object? sender, EventArgs eventArgs)
    {
        if (!TryReadBounds(out var lowerBound, out var upperBound))
        {
            return;
        }

        _stopRequested = false;
        _primeCount = 0;
        _primeNumbersListBox.Items.Clear();
        _countLabel.Text = "Знайдено: 0";

        SetGenerationControls(isRunning: true);

        _statusLabel.Text = upperBound.HasValue
            ? $"Генерація у діапазоні {lowerBound}–{upperBound.Value}..."
            : $"Генерація від {lowerBound} без верхньої межі...";

        _workerThread = new Thread(
            () => GeneratePrimeNumbers(lowerBound, upperBound))
        {
            IsBackground = true,
            Name = "PrimeGeneratorThread"
        };

        _workerThread.Start();
    }

    private bool TryReadBounds(out long lowerBound, out long? upperBound)
    {
        lowerBound = 2;
        upperBound = null;

        if (!string.IsNullOrWhiteSpace(_lowerBoundTextBox.Text)
            && !long.TryParse(_lowerBoundTextBox.Text, out lowerBound))
        {
            ShowValidationError("Нижня межа повинна бути цілим числом.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_upperBoundTextBox.Text))
        {
            if (!long.TryParse(_upperBoundTextBox.Text, out var parsedUpperBound))
            {
                ShowValidationError("Верхня межа повинна бути цілим числом.");
                return false;
            }

            upperBound = parsedUpperBound;
        }

        lowerBound = Math.Max(2, lowerBound);

        if (upperBound.HasValue && upperBound.Value < lowerBound)
        {
            ShowValidationError(
                "Верхня межа повинна бути не меншою за нижню.");
            return false;
        }

        return true;
    }

    private void GeneratePrimeNumbers(long lowerBound, long? upperBound)
    {
        var batch = new List<long>(100);
        var updateTimer = Stopwatch.StartNew();
        var current = lowerBound;

        while (!_stopRequested
               && (!upperBound.HasValue || current <= upperBound.Value))
        {
            if (PrimeGenerator.IsPrime(current))
            {
                batch.Add(current);
                Interlocked.Increment(ref _primeCount);
            }

            if (batch.Count >= 100 || updateTimer.ElapsedMilliseconds >= 200)
            {
                PostBatchToInterface(batch);
                batch.Clear();
                updateTimer.Restart();
            }

            if (current == long.MaxValue)
            {
                break;
            }

            current++;

            if ((current & 4095) == 0)
            {
                Thread.Yield();
            }
        }

        PostBatchToInterface(batch);

        PostToInterface(
            () =>
            {
                SetGenerationControls(isRunning: false);
                _statusLabel.Text = _stopRequested
                    ? "Генерацію зупинено користувачем."
                    : "Генерацію завершено.";
                _workerThread = null;
            });
    }

    private void PostBatchToInterface(List<long> batch)
    {
        var values = batch.ToArray();
        var totalCount = Interlocked.Read(ref _primeCount);

        PostToInterface(
            () =>
            {
                if (values.Length > 0)
                {
                    _primeNumbersListBox.BeginUpdate();

                    foreach (var value in values)
                    {
                        _primeNumbersListBox.Items.Add(value);
                    }

                    _primeNumbersListBox.EndUpdate();
                    _primeNumbersListBox.TopIndex =
                        _primeNumbersListBox.Items.Count - 1;
                }

                _countLabel.Text = $"Знайдено: {totalCount}";
            });
    }

    private void PostToInterface(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // Вікно вже закривається; фоновий потік завершиться через прапорець.
        }
    }

    private void StopGeneration(object? sender, EventArgs eventArgs)
    {
        _stopRequested = true;
        _stopButton.Enabled = false;
        _statusLabel.Text = "Зупинка потоку...";
    }

    private void SetGenerationControls(bool isRunning)
    {
        _startButton.Enabled = !isRunning;
        _stopButton.Enabled = isRunning;
        _lowerBoundTextBox.Enabled = !isRunning;
        _upperBoundTextBox.Enabled = !isRunning;
    }

    private static void ShowValidationError(string message)
    {
        MessageBox.Show(
            message,
            "Помилка введення",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        _stopRequested = true;

        if (_workerThread is { IsAlive: true })
        {
            _workerThread.Join(millisecondsTimeout: 1000);
        }
    }
}
