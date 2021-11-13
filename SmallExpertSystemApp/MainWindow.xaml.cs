using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;


namespace SmallExpertSystemApp
{
    public partial class MainWindow : Window
    {
        private List<String> QuestionNames = new List<string>();
        private List<Answer> Answers = new List<Answer>();

        private float P_max = 0.95f;
        private float P_min = 0.05f;
        private float P_average = 0.5f;

        private int QuestionId = 0;

        public MainWindow()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            InitializeComponent();
            Title = this.FindResource("Title").ToString();
        }

        private void TopMenuOpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            string str = "";
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = "C:\\";
            openFileDialog.Filter = "База данных МЭС (*.mkb)|*.mkb|Все файлы (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                QuestionNames = new List<string>();
                Answers = new List<Answer>();
                FillInternalVariables(openFileDialog.FileName);

                InitQuestionNamesListView();
                UpdateAnswerNamesListView();

                QuestionTextBlock.Text = QuestionNames[0];
                ApplyAnswerButton.IsEnabled = true;
            }

        }

        private void FillInternalVariables(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            Title = this.FindResource("Title").ToString() + ": " + fileInfo.Name;
            Encoding encoding = Encoding.UTF8;
            if (fileInfo.Extension.Contains(".mkb"))
                encoding = Encoding.GetEncoding("windows-1251");

            string input;
            using (var f = new StreamReader(filePath, encoding))
            {
                string bdName = "";
                input = f.ReadLine();//Тема базы знаний
                while (!input.Equals(""))
                {
                    bdName += input + "; ";
                    input = f.ReadLine();
                }
                bdName = bdName.Substring(0, bdName.Length - 2);
                Title = this.FindResource("Title").ToString() + ": " + bdName;

                input = f.ReadLine();//Вопросы:
                input = f.ReadLine();//первый вопрос
                while (!input.Equals(""))
                {
                    QuestionNames.Add(input);
                    input = f.ReadLine();
                }

                input = f.ReadLine();//первая строка ответов
                while (input != null)
                {
                    string[] row = input.Split(',');

                    Answer answer = new Answer(row[0], row[1]);
                    List<Probability> probs = new List<Probability>();

                    for (int i = 2; i < row.Length; i += 3)
                    {

                        string ansNumber = row[i].Trim();
                        string trueProb = row[i + 1].Trim();
                        string falseProb = row[i + 2].Trim();

                        Probability prob = new Probability(ansNumber, trueProb, falseProb);
                        probs.Add(prob);
                    }
                    answer.Probabilities = probs;
                    Answers.Add(answer);
                    input = f.ReadLine();
                }
            }
            float max = 0f;
            float min = 1f;
            foreach (Answer ans in Answers)
            {
                foreach (Probability p in ans.Probabilities)
                {
                    if (p.True > max)
                        max = p.True;
                    if (p.False < min)
                        min = p.False;
                }
            }
            P_max = max;
            P_min = min;
            P_average = (max - min) / 2;

        }

        private void InitQuestionNamesListView()
        {
            if (QuestionNames != null)
            {
                QuestionNamesListView.Items.Clear();
                int i = 1;
                foreach (string questionName in QuestionNames)
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = $"{i}. {questionName}";
                    QuestionNamesListView.Items.Add(tb);
                    i++;
                }
            }
        }
        private void UpdateAnswerNamesListView()
        {
            if (Answers != null)
            {
                AnswerNamesListView.Items.Clear();
                foreach (Answer answer in Answers)
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = $"({answer.Value:f4}) {answer.Name}";
                    AnswerNamesListView.Items.Add(tb);
                }
            }
        }


        private void TopMenuExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AnswerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!(Char.IsDigit(e.Text, 0) || (e.Text == ".") || (e.Text == ",")
               && (!AnswerTextBox.Text.Contains(".")
               && AnswerTextBox.Text.Length != 0)))
            {
                e.Handled = true;
            }
        }

        private void ApplyAnswerButton_Click(object sender, RoutedEventArgs e)
        {
            if (QuestionId < QuestionNames.Count)
            {
                string input = AnswerTextBox.Text.ToString();
                if (input != "")
                {
                    if (QuestionId < QuestionNames.Count)
                    {
                        float value = (float)Convert.ToDouble(input.Replace('.', ','));
                        CalculateThings(value, QuestionId);
                        UpdateAnswerNamesListView();
                        QuestionId++;
                        if (QuestionId < QuestionNames.Count)
                        {
                            QuestionTextBlock.Text = QuestionNames[QuestionId];
                            AnswerTextBox.Text = "";
                        }
                        else
                        {
                            ApplyAnswerButton.IsEnabled = false;
                            AnswerTextBox.Text = "";

                            ShowTheBestOption();
                        }
                    }
                }
            }
        }
        private void ShowTheBestOption()
        {
            float theBestValue = 0;
            int theBestId = 0;
            int i = 0;
            foreach (Answer ans in Answers)
            {
                if (theBestValue < ans.Value)
                {
                    theBestValue = ans.Value;
                    theBestId = i;
                }

                i++;
            }

            TextBlock theBestTb = (TextBlock)AnswerNamesListView.Items[theBestId];
            theBestTb.Foreground = Brushes.Red;

        }
        private void CalculateThings(float answerValue, int questionId)
        {
            if (answerValue < P_average)
            {
                foreach (Answer ans in Answers)
                {
                    Probability p = ans.Probabilities[questionId];
                    float p_apr = ans.Value;
                    float p_hne = ((1 - p.True) * p_apr) / ((1 - p.True) * p_apr + (1 - p.False) * (1 - p_apr));

                    float min = P_min;
                    float max = P_max;

                    float p_hPrev = ans.Value;
                    float p_h = p_hne + ((answerValue - min) * (p_hPrev - p_hne)) / (P_average - min);
                    ans.Value = p_h;
                }
            }
            else
            {
                foreach (Answer ans in Answers)
                {
                    Probability p = ans.Probabilities[questionId];
                    float p_apr = ans.Value;
                    float p_he = (p.True * p_apr) / (p.True * p_apr + p.False * (1 - p_apr));

                    float min = P_min;
                    float max = P_max;

                    float p_hPrev = ans.Value;
                    float p_h = p_hPrev + ((answerValue - P_average) * (p_he - p_hPrev)) / (max - P_average);
                    ans.Value = p_h;
                }
            }
        }
    }
}
class Answer
{
    public string Name;
    public float Average;
    public float Value = 0;
    public List<Probability> Probabilities;
    public Answer(string _name, string _average)
    {
        Name = _name;
        Average = (float)Convert.ToDouble(_average.Replace('.', ','));
        Value = Average;
    }
}
class Probability
{
    public int Number;
    public float True;
    public float False;
    public Probability(string _number, string _true, string _false)
    {
        Number = Convert.ToInt32(_number);
        True = (float)Convert.ToDouble(_true.Replace('.', ','));
        False = (float)Convert.ToDouble(_false.Replace('.', ','));
    }
}