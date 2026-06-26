using System;
using System.Collections.Generic;
using System.Text;

namespace Goke.Core.Extensions
{
    public static class StringBuilderExtensions
    {
        //
        const string SPECIAL = "@#$%&+=?<>!/~-";
        const string ALPHABETH = "ABCDEFGHIJKLMNPQRSTUVWXYZ";
        const string LOWERALPHABETH = "abcdefghkmnpqswxyz";
        const string NUMBER = "0123456789";
        static Random random = new();

        extension(StringBuilder sb)
        {
            public void InsertLowerAlphabeth()
            {
                var k = random.Next(LOWERALPHABETH.Length);
                var s = LOWERALPHABETH.ElementAt(k).ToString();
                k = random.Next(sb.Length);
                sb.Insert(k, s);
            }

            public void InsertUpperAlphabeth()
            {
                int k = random.Next(ALPHABETH.Length);
                var s = ALPHABETH.ElementAt(k).ToString();
                k = random.Next(sb.Length);
                sb.Insert(k, s);
            }

            public void InsertSpecialCharacter()
            {
                int k = random.Next(SPECIAL.Length);
                var s = SPECIAL.ElementAt(k).ToString();
                k = random.Next(sb.Length);
                sb.Insert(k, s);
            }

            public void InsertNumber()
            {
                int k = random.Next(NUMBER.Length);
                var s = NUMBER.ElementAt(k).ToString();
                k = random.Next(sb.Length);
                sb.Insert(k, s);
            }

        }

        extension(StringBuilder) 
        { 
            public static string GeneratePassword(int numChars = 12, int lowers=0, int uppers = 1, int specials = 1, int digits = 1)
            {
                StringBuilder password = new StringBuilder();

                lowers = numChars - (uppers + specials + digits);

                // insert a lowercase character at a random position in the password
                for (int i = 0; i < lowers; i++)
                {
                    password.InsertLowerAlphabeth();
                }

                // insert a special character at a random position in the password
                for (int i = 0; i < specials; i++)
                {
                    password.InsertSpecialCharacter();
                }

                // insert an uppercase character at a random position in the password
                for (int i = 0; i < uppers; i++)
                {
                    password.InsertUpperAlphabeth();
                }

                // insert an number character at a random position in the password
                for (int i = 0; i < digits; i++)
                {
                    password.InsertNumber();
                }

                return password.ToString();
            }

            public static string GeneratePin(int digits = 9)
            {
                StringBuilder pin = new StringBuilder(string.GenerateDigits(digits));

                // insert a special character at a random position in the pin
                // pin.InsertSpecialCharacter();

                // insert an uppercase character at a random position in the pin
                pin.InsertUpperAlphabeth();

                // insert a lowercase character at a random position in the pin
                pin.InsertLowerAlphabeth();

                return pin.ToString();
            }

        }

    }
}

