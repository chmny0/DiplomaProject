using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfPlannerApp.Helpers
{
    public static class MaskedInputHelper
    {
        public static void PhoneMask(TextBox textBox)
        {
            textBox.MaxLength = 18;

            textBox.PreviewTextInput += (s, e) =>
            {
                if (!Regex.IsMatch(e.Text, @"^\d+$"))
                {
                    e.Handled = true;
                    return;
                }

                string digits = Regex.Replace(textBox.Text, @"[^\d]", "");

                if (digits.Length >= 11)
                {
                    e.Handled = true;
                    return;
                }

                digits += e.Text;

                int caret = textBox.CaretIndex;
                textBox.Text = FormatPhone(digits);

                textBox.CaretIndex = Math.Min(caret + 2, textBox.Text.Length);

                e.Handled = true;
            };

            textBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Back)
                {
                    string digits = Regex.Replace(textBox.Text, @"[^\d]", "");

                    if (digits.Length <= 1)
                    {
                        textBox.Text = "+7 (";
                        textBox.CaretIndex = 4;
                        e.Handled = true;
                        return;
                    }

                    digits = digits.Substring(0, digits.Length - 1);

                    textBox.Text = FormatPhone(digits);
                    textBox.CaretIndex = textBox.Text.Length;

                    e.Handled = true;
                }

                if (e.Key == Key.Delete)
                {
                    string digits = Regex.Replace(textBox.Text, @"[^\d]", "");
                    int caret = textBox.CaretIndex;

                    int digitsBeforeCaret = 0;
                    for (int i = 0; i < Math.Min(caret, textBox.Text.Length); i++)
                    {
                        if (char.IsDigit(textBox.Text[i]))
                            digitsBeforeCaret++;
                    }

                    if (digitsBeforeCaret < digits.Length)
                    {
                        digits = digits.Remove(digitsBeforeCaret, 1);
                        textBox.Text = FormatPhone(digits);

                        int newCaret = 0;
                        int digitCount = 0;
                        for (int i = 0; i < textBox.Text.Length; i++)
                        {
                            if (char.IsDigit(textBox.Text[i]))
                            {
                                digitCount++;
                                if (digitCount == digitsBeforeCaret + 1)
                                {
                                    newCaret = i + 1;
                                    break;
                                }
                            }
                        }
                        textBox.CaretIndex = newCaret > 0 ? newCaret : textBox.Text.Length;
                    }

                    e.Handled = true;
                }

                if (e.Key == Key.Space || e.Key == Key.OemMinus)
                {
                    e.Handled = true;
                }
            };

            if (string.IsNullOrWhiteSpace(textBox.Text) || textBox.Text == "+7 (")
            {
                textBox.Text = "+7 (";
                textBox.CaretIndex = 4;
            }
        }

        private static string FormatPhone(string digits)
        {
            if (string.IsNullOrEmpty(digits) || digits == "7")
                return "+7 (";

            string number = digits.StartsWith("7") ? digits.Substring(1) : digits;

            string result = "+7 (";

            if (number.Length > 0)
            {
                result += number.Substring(0, Math.Min(3, number.Length));
            }

            if (number.Length > 3)
            {
                result += ") " + number.Substring(3, Math.Min(3, number.Length - 3));
            }
            else if (number.Length >= 3)
            {
                result += ") ";
            }

            if (number.Length > 6)
            {
                result += "-" + number.Substring(6, Math.Min(2, number.Length - 6));
            }

            if (number.Length > 8)
            {
                result += "-" + number.Substring(8, Math.Min(2, number.Length - 8));
            }

            return result;
        }

        public static string CleanPhone(string? masked)
        {
            if (string.IsNullOrWhiteSpace(masked)) return "";
            string digits = Regex.Replace(masked, @"[^\d]", "");
            return digits.StartsWith("7") ? "+7" + digits.Substring(1) : "+7" + digits;
        }

        public static void InnMask(TextBox textBox, bool isLegal = true)
        {
            textBox.MaxLength = isLegal ? 10 : 12;
            textBox.PreviewTextInput += (s, e) =>
            {
                e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
            };
        }

        public static void KppMask(TextBox textBox)
        {
            textBox.MaxLength = 9;
            textBox.PreviewTextInput += (s, e) =>
            {
                e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
            };
        }

        public static void PassportMask(TextBox textBox)
        {
            textBox.MaxLength = 11;
            textBox.PreviewTextInput += (s, e) =>
            {
                if (!Regex.IsMatch(e.Text, @"^\d+$"))
                {
                    e.Handled = true;
                    return;
                }
                if (textBox.Text.Length == 4)
                {
                    textBox.Text += " ";
                    textBox.CaretIndex = 5;
                }
            };
            textBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Back && textBox.CaretIndex == 5)
                {
                    textBox.Text = textBox.Text.Substring(0, 4);
                    textBox.CaretIndex = 4;
                    e.Handled = true;
                }
            };
        }

        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return true;
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }
    }
}