using System;

namespace TestLibrary
{
    class Program
    {
        static void Main(string[] args)
        {
            Speaker speaker = new Speaker();
            speaker.Use();
            int times = 3;
            Greet(times);
        }

        static void Greet(int times)
        {
            for (int i = 0; i < times; i++)
            {
                Console.WriteLine("Hello World!");
            }
        }

        public class Repeater
        {
            public static void Repeat(string phrase)
            {
                Console.WriteLine(phrase);
            }
        }
    }

    public class Speaker
    {
        public string phrase = "This is a test";

        public void Use(int times = 1)
        {
            Console.WriteLine(phrase);
            Start();
        }

        protected void Start()
        {
            Console.WriteLine("Speaker is on");
        }
    }
}
