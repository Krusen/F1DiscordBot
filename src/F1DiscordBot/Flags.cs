namespace F1DiscordBot
{
    public static class Flags
    {
        public static string GetFlag(string nationality)
        {
            string code;
            switch (nationality.ToLower())
            {
                case "american": code = "us"; break;
                case "australian": code = "au"; break;
                case "austrian": code = "au"; break;
                case "belgian": code = "be"; break;
                case "brazilian": code = "br"; break;
                case "british": code = "gb"; break;
                case "canadian": code = "ca"; break;
                case "danish": code = "dk"; break;
                case "dutch": code = "nl"; break;
                case "finnish": code = "fi"; break;
                case "french": code = "fr"; break;
                case "german": code = "de"; break;
                case "indian": code = "in"; break;
                case "indonesian": code = "id"; break;
                case "italian": code = "it"; break;
                case "japanese": code = "jp"; break;
                case "mexican": code = "mx"; break;
                case "polish": code = "pl"; break;
                case "russian": code = "ru"; break;
                case "spanish": code = "es"; break;
                case "swedish": code = "se"; break;
                case "swiss": code = "ch"; break;
                default:
                    return ":gay_pride_flag:";
            }

            return $":flag_{code}:";
        }
    }
}
