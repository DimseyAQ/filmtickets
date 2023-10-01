using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;

#pragma warning disable CS0675 // Disables annoying warning about something that doesnt matter

namespace filmtickets
{
    public static class Program
    {
        static string ToTitle(string str)
            => System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());
        // Function to convert a string into title case

        static int Clamp(int value, int min, int max)
            => (value <= min) ? min : ((value >= max) ? max : value);
        // Function to ensure a value is in-between 2 others, if not- it will return whatever number its closest to (min or max)

        static void Flush(int startLine = 0)
        // Function to "flush" (or replace) all text starting from a certain line (no stuttering like Console.Clear())
        {
            for (int i = startLine; i < Console.WindowTop + Console.WindowHeight; i++) // Iterate through all lines of screen
                Write(" ".PadRight(Console.WindowWidth), ConsoleColor.White, 0, i);
        }

        static void Write(string message, ConsoleColor text_color, int x, int y, bool clear_line = false)
        // VERY Reusable function to simplify writing to console
        {
            int currentLine = Console.CursorTop; // Save current cursor position so we can restore it at the end
            if (clear_line) // Clears whole line where text will be placed before placing it
            {
                Console.SetCursorPosition(0, y);
                Console.Write(" ".PadRight(Console.WindowWidth)); // Writes a string full of spaces to the same width as the window
            }

            Console.SetCursorPosition(
                Clamp(x, 0, Console.WindowWidth), Clamp(y, 0, Console.WindowHeight) // Ensure new position is within the consoles window
            );

            Console.ForegroundColor = text_color; Console.Write(message); Console.ResetColor();
            Console.SetCursorPosition(0, currentLine); // Reset Cursor Position
        }

        struct TimeStamp
        // A struct to hold booked seats as long and timing
        {
            public long seats;
            public DateTime date;
            public TimeStamp(long seats, DateTime date)
            {
                this.seats = seats;
                this.date = date;
            }
        }

        struct SeatView
            // (ended up not being used but i figured i
        {
            private long seatData;
            public SeatView(long seatData)
            {
                this.seatData = seatData;
            }
        }

        public struct CacheState
        // A struct handling I/O operations for CACHE aswell as already booked tickets
        {
            private readonly Dictionary<string, List<TimeStamp>> bookedTickets;
            
            

            public CacheState(int _ = -1) // Have to include a parameter despite not needing one as C# doesnt allow otherwise in this version
            {
                bookedTickets = new Dictionary<string, List<TimeStamp>>();

                if (!System.IO.File.Exists("CACHE"))
                    System.IO.File.Create("CACHE").Close(); // The .Close() must be called as File.Create returns a FileStream object which is not what we want
                else
                {
                    foreach (string line in File.ReadAllLines("CACHE")) // Iterate through every line of the CACHE file
                    {
                        if (line.Length < 5) // Minimum length a line SHOULD have
                            continue;
                        List<TimeStamp> timeStamps = new List<TimeStamp>();

                        foreach (
                            Group group in Regex.Matches( // Regex.Matches will find all occurances of a regex experesion
                                line.Substring(line.IndexOf('X') - 1, line.Length - line.IndexOf('X') + 1), // All text apart from the initial name
                                @"\<([^>]*)\>" // Regex expression to capture text like "<{value}>
                            )
                        )
                        {
                            string rawData = group.Value.Substring(1, group.Value.Length - 2); // Remove the < and > on the outside
                            TimeStamp timeStamp = new TimeStamp( // Create a time stamp object through the data
                                    Convert.ToInt64(rawData.Split('.')[0]), new DateTime(Convert.ToInt64(rawData.Split('.')[1]))
                                    // Splits the string into two parts the left being the seat data and the right being the datetime ticks
                            );
                            timeStamps.Add(timeStamp);
                        }

                        bookedTickets[line.Remove(line.IndexOf('X'))] = timeStamps;
                        
                    }
                }
            }


            public void Save()
            // A function that will save all data back to CACHE
            {
                StringBuilder fileData = new StringBuilder();

                foreach (KeyValuePair<string, List<TimeStamp>> entry in bookedTickets) // Iterate through each key and its value
                {
                    string screeningData = "";
                    foreach (TimeStamp timeStamp in entry.Value)
                        screeningData += $"<{timeStamp.seats}.{timeStamp.date.Ticks}>";
                    fileData.AppendLine($"{entry.Key}X({screeningData})");
                }
                System.IO.File.WriteAllBytes(
                    "CACHE", Encoding.UTF8.GetBytes(fileData.ToString().ToCharArray()) // Converts strings to a byte array so we can write bytes
                );
            }


            public long GetSeating(string name, DateTime date)
            // A function that will return any booked seating data or 0, making it safe to use | operator with any other long
            { 
                if (bookedTickets.ContainsKey(name))
                    try
                    {
                        return bookedTickets[name.ToLower()].First(
                            (TimeStamp ts) => ts.date.Ticks == date.Ticks
                        ).seats;
                    }
                    catch {} // Does nothing in catch to "fall through" to bottom line
                return 0;
            }


            public void Add(Film film, long seats)
            // A function that adds a users booked seats  to bookedTickets
            {
                List<TimeStamp> timeStamps;
                if (bookedTickets.ContainsKey(film.name))
                {
                    timeStamps = bookedTickets[film.name];

                    try
                    {
                        TimeStamp existingTimeStamp = timeStamps.First(  // Checks if any 
                            (TimeStamp timeStamp) => timeStamp.date.Ticks == film.screeningTime.Ticks
                        );
                        if (existingTimeStamp.seats != seats)
                            existingTimeStamp.seats |= seats; // Will combine both seating datas and make them overlap
                        return;
                    }
                    catch (InvalidOperationException) { } // Do nothing upon catch to "fall through" to the last 2 statements
                }
                else
                    timeStamps = new List<TimeStamp>();

                timeStamps.Add(
                    new TimeStamp(
                        seats, new DateTime( // Ensure only DD/MM/YY data is saved
                            film.screeningTime.Year,
                            film.screeningTime.Month,
                            film.screeningTime.Day,
                            0, 0, 0
                        )
                    )
                );
                bookedTickets[film.name] = timeStamps;
            }
        };

        public static CacheState Cache = new CacheState(1); // Create a Global CacheState


        public struct Film
        // A struct representing a Screening of a film
        {
            public string name; // Title of film in lowercase

            public DateTime screeningTime; // Time the film is screened at

            public long filmData;
            // filmData is a 64 bit int that holds all data surrounding the screening
            // 1111 0000 1111 0000 1111 0000 1111 0000 1111 0000 1111 0000 1111 0000 1111 0000
            //  																		    XX -> Age rating(ranges from 0 to 3, representing U 12A 15 and 18)
            //                      XXX XXXX XXXX XXXX XXXX XXXX XXXX XXXX XXXX XXXX XXXX XX -> Occupied Seating(each bit represents a bool value in a 5 by 9 array)
            //                XXXX X -> Quality(ranges from 0 to 31 inclusive, used for seat population and star ratings)
            //           XXXX -> Screen (ranges from 0 to 15 but will have 1 added to it, represents the screen its at)
            // XXXX XXXX -> Unused bits
            //
            // Why have I done all this instead of individual variables and arrays? for fun and headaches

            public Film(string name, DateTime? screeningTime = null)
            // Film constructor for generating a new film, with or without a given time
            {
                this.name = name;

                this.screeningTime = screeningTime ?? new DateTime( // Checks if screeningTime is null, if it is it will return DateTime.Now with hours truncated
                    DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0
                );

                this.filmData = name[name.Length - 1]; // Add a teeny degree of extra psuedo-randomness

                for (int i = 0; i < name.Length; i++)
                    filmData *= 0x5851F42D4C957F2D * (name[i] ^ (name[i] >> 62)) + (i - 1); // Hash a string to give a varied but not unique number

                filmData |= Cache.GetSeating(name, this.screeningTime);

                PopulateSeats(); // Expand or shrink the density of seats
            }
            public Film(string name, long filmData, DateTime? screeningTime = null)
            // Film constructor for generating a new film, with or without a given time
            {
                this.name = name;
                this.screeningTime = screeningTime ?? new DateTime(
                    DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0
                );
                this.filmData = filmData | Cache.GetSeating(name, this.screeningTime);
            }
            public Film(string name, int[,] seating_arrangements, string age_rating, int quality, DateTime screeningTime, bool populateSeats = false)
            // Film Constructor for manually creating specific films
            {

                if (seating_arrangements.GetLength(0) != 5 || seating_arrangements.GetLength(1) != 9)
                    // Checks that the arrays length is 5 and the arrays 1st nested array is a length of 9
                    // Also has a double intended effect of checking if array is 2 dimenseonal, as seating_arrangements.GetLength(1) 
                    // will throw an error if it is not 2 Dimensional
                    throw new ArgumentException("seating_arrangements must be 2 dimenseonal array of ints in the arrangement [5,9]");


                this.name = name;

                string seating_data = "";
                for (int r = 4; r > -1; r--)
                    for (int c = 8; c > -1; c--)
                        seating_data += seating_arrangements[r, c] != 0 ? '1' : '0'; // Construct a string representing a 64 bit int from an array of bools

                this.screeningTime = screeningTime;

                this.filmData = ((Convert.ToInt64(seating_data, 2) & 0x1FFFFFFFFFFF) << 2) | Cache.GetSeating(name, this.screeningTime);
                // Convert the string to 64 bit int (will never throw an error), then get the first 45 bits from it,
                // and finally bit shift it all to the left by 2 to get it into the right format


                switch (age_rating)
                {
                    case "12":
                        filmData |= 1; break;
                    case "15":
                        filmData |= 2; break;
                    case "18":
                        filmData |= 3; break;
                    default: // If U is given or something that doesnt conform to the past 3 ratings, add no additional data- meaning the rating will be a U
                        break;
                }

                filmData |= ((long)quality % 32) << 47; // Format the quality variable into the correct bits

                if (populateSeats)
                    PopulateSeats();
            }


            public bool this[int c, int r]
            // An operator overloader that gives the Film struct an array-like interface
            {
                get // Function will run if Film[x] is called, and has to return a boolean
                    => (filmData >> (2 + (r % 5 * 9) + c % 9) & 1) != 0;
                // In order of precedence (Brackets, Indices, etc)
                // r % 5 and c % 9 will ensure -1 < ROW < 5 and -1 < COLUMN < 9
                // (r % 5 * 9) + c % 9 will create an int representing ROW and COLUMN,
                // that also represents what bit to get out of the 45 bit that represent the seating data

                set // Function will run if Film[x] = true/false is called, with value representing true/false
                {
                    c %= 9; r %= 5; // Ensure row is 0 - 4 inclusive and column is 0 - 8 inclusive
                    if (value)
                        filmData |= 0b10L << (r * 9) + c + 1;
                    // 0b10 << (r * 9) + c + 1 will create a long with only the Nth bit turned on, where N is an int representing ROW and COLUMN
                    // The |= operator will turn this bit on as if 1 of the Nth bit from either filmData or the previously made long are 1, it will set filmDatas to 1
                    else
                        filmData &= ~(0b10L << (r * 9) + c + 1);
                    // The ~() around 0b10 << (r * 9) + c + 1 will create a long with only the Nth bit turned off, where N is an int representing ROW and COLUMN
                    // The &= operator will turn this bit off, as it will only turn it on if both filmData and the previously made long
                    // have the Nth bit turned on, which the previously made long will NEVER have turned on
                }
            }


            private void PopulateSeats()
            // A private function that will puesdo-randomly add or remove 
            {
                Random prng = new Random((int)screeningTime.Ticks >> 32); // Add some variation depending on different screening times by using time as seed
                double quality = GetQuality();

                if (quality <= 7)
                {
                    filmData &= ~(0x7FFFFFFFFFFC); // Sets all bits where seating data is stored to 0
                    for (int i = 0; i < quality; i++)
                        this[prng.Next(0, 9), prng.Next(0, 5)] = true;
                }
                else if (quality > 7 && quality < 29.4) // IF quality is above 25% OR below 95% THEN add an amount of seats equal to quality randomly 
                {
                    for (int i = 0; i < quality / 2; i++)
                        this[prng.Next(0, 9), prng.Next(0, 5)] = true;
                }
                else if (quality >= 29.4) // IF quality is above 95% THEN make all seats occupied and randomly make some free
                {
                    filmData |= 0x7FFFFFFFFFFC; // Sets all bits where seating data is stored to 1
                    for (int i = 0; i < quality % 8; i++)
                        this[prng.Next(0, 9), prng.Next(0, 5)] = false;
                }


            }


            public void SelectSeating(bool showingToday = false)
            // A function that will start the booking process of a film
            {

                Console.Clear();
                Write(
                    "------------------------------------------------------------------\n" +
                    "|" + ToTitle((name.PadLeft(name.Length + ((64 - name.Length) / 2), '.')).PadRight(64, '.')) + "|\n" +
                    //                  ^ Ensure film title is in the center of a 64-char-wide box
                    "------------------------------------------------------------------\n",
                    ConsoleColor.White, 0, 0
                );

                Write("A B C D E F G H I", ConsoleColor.Cyan, 2, 3); // Set up X axis
                for (int r = 0; r < 5; r++)
                {
                    Write((r + 1).ToString(), ConsoleColor.Cyan, 0, 4 + r); // Set up Y axis
                    for (int c = 0; c < 9; c++)
                        Write(
                            (this[c, r]) ? "X " : ". ", (this[c, r]) ? ConsoleColor.DarkRed : ConsoleColor.DarkGray,
                            // Writes X if seat is occupied and . if not
                            2 + (c * 2), 4 + r
                        );
                }
                Write(GetStarRating(), ConsoleColor.Yellow, 20, 4);
                Write(" | ", ConsoleColor.White, 25, 4);
                Write(GetAgeRating(), GetAgeRatingColor(), 28, 4);

                while (true) // Stage 1 - Ask if user would like to book tickets (if they can)
                {
                    Write($"SEATING ", ConsoleColor.DarkGray, 20, 5);
                    Write(
                        $"{(!IsAvailable() ? "UN" : "")}AVAILABLE SEATS",
                        IsAvailable() ? ConsoleColor.Green : ConsoleColor.Red,
                        20, 5
                    );
                    Write(
                        $"{GetAvailability()} Seat{(GetAvailability() == 1 ? "" : "s")} left ",
                        IsAvailable() ? ConsoleColor.DarkGreen : ConsoleColor.DarkRed,
                        20, 6
                    );
                    Write($"Screen {((filmData & 0xF0000000000000) >> 52) + 1,2} on ", ConsoleColor.Gray, 20, 7);
                    // Get the screening data from filmData and display it
                    Write($"{screeningTime.Day,2}/{screeningTime.Month,2}/{screeningTime.Year,4}".Replace(' ', '0'), ConsoleColor.Yellow, 33, 7);

                    Write("Would you like to book tickets? (y/n) ", ConsoleColor.White, 0, 10);
                    Flush(11);

                    do // Wait for input
                    {
                        ConsoleKey key = Console.ReadKey(true).Key;
                        if (key == ConsoleKey.N)
                            return; // Returns back to homepage screen
                        else if (key == ConsoleKey.Y)
                            break; // Continues on
                    } while (true);


                    if (ChooseSeating()) // IF second stage of booking is successful THEN revert to homepage
                        return;
                }
            }


            private bool ChooseSeating()
            // A function to navigate over the seats and placing bookings (second stage of SelectSeating)
            {
                List<int> booked_tickets = new List<int>(); // List of ints representing selected seats (INT -> (ROW * 9) + COLUMN)
                int sc = 0, sr = 0; // Ints represeting SEAT COLUMN, SEAT ROW
                while (true) // Outer loop is here to allow for "back tracking" between the stages
                {
                    Write("ARROW KEYS to navigate seats          ", ConsoleColor.White, 0, 10);
                    Write("SPACE to place a selection", ConsoleColor.White, 0, 11);
                    Write("ENTER to confirm selection", ConsoleColor.White, 0, 12);
                    Write("ESC to cancel", ConsoleColor.White, 0, 13);

                    Write("◆ ",
                        booked_tickets.Contains(0) ? ConsoleColor.Blue : (
                            (this[0, 0]) ? ConsoleColor.Red : ConsoleColor.Green
                        ),
                        // Checks if current seat is currently selected by you and if it isnt, then checks if its free or occupied
                        // BLUE indicates youve selected it
                        // RED indicates the seat is occupied
                        // GREEN indicates the seat is free
                        2 + (sc * 2), 4 + sr
                    );

                    while (true)
                    {
                        ConsoleKey key = Console.ReadKey(true).Key;

                        if (booked_tickets.Contains((sr * 9) + sc))
                            Write("0 ", ConsoleColor.DarkCyan, 2 + (sc * 2), 4 + sr); // IF youve selected this seat write a blue 0
                        else
                            Write(
                                (this[sc, sr]) ? "X " : ". ", (this[sc, sr]) ? (ConsoleColor.DarkRed) : ConsoleColor.DarkGray,
                                // Write a red X if seat is occupied, Write a gray . if seat is free
                                2 + (sc * 2), 4 + sr
                            );


                        if (key == ConsoleKey.Spacebar && !this[sc, sr]) // IF SPACEBAR is pressed (space is selection hot key) AND seat is free
                            if (booked_tickets.Contains((sr * 9) + sc)) // Check seat is already selected and if it is, unselect it
                                booked_tickets.Remove((sr * 9) + sc);
                            else // Otherwise, select it
                                booked_tickets.Add((sr * 9) + sc);
                        else if (key == ConsoleKey.Enter) // IF ENTER is pressed (enter is confirm hot key)
                        {
                            if (booked_tickets.Count() == 0) // Check if user has selected any seats
                                Write("You havent selected any seats!", ConsoleColor.Red, 20, 8);
                            else if (ChooseTickets(sc, sr, booked_tickets))
                                // Move to the next stage, it will return true if everything is booked and false if something went wrong or broke
                                return true;
                            else
                                break; // Restart loop
                        }
                        else if (key == ConsoleKey.Escape) return false; // IF ESCAPE is pressed (escape is cancel hot key) go back a stage
                        else if (key == ConsoleKey.UpArrow && sr > 0) sr--; // UP ARROW pressed -> move up 1 seat IF not at top already
                        else if (key == ConsoleKey.DownArrow && sr < 4) sr++; // DOWN ARROW pressed -> move down 1 seat IF not at bottom already
                        else if (key == ConsoleKey.LeftArrow && sc > 0) sc--; // LEFT ARROW pressed -> move left 1 seat IF not at left border already
                        else if (key == ConsoleKey.RightArrow && sc < 8) sc++; // RIGHT ARROW pressed -> move right 1 seat IF not at right border already

                        Write("◆ ",
                            booked_tickets.Contains((sr * 9) + sc) ? ConsoleColor.Blue : (
                                (this[sc, sr]) ? ConsoleColor.Red : ConsoleColor.Green
                            ), 2 + (sc * 2), 4 + sr
                        );

                        Write($"SEAT {(char)(sc + 65)}{sr + 1} - ", ConsoleColor.DarkGray, 20, 5);
                        Write(
                            booked_tickets.Contains((sr * 9) + sc) ? "SELECTED       " : (
                                (this[sc, sr]) ? "ALREADY BOOKED" : "FREE SEATING   "
                            ),
                            booked_tickets.Contains((sr * 9) + sc) ? ConsoleColor.Blue : (
                                (this[sc, sr]) ? ConsoleColor.Red : ConsoleColor.Green
                            ),
                            30, 5
                        );
                    }
                }
            }
            private bool ChooseTickets(int sc, int sr, List<int> booked_tickets)
            // A function to distribute tickets for each of your booked seats (third stage of SelectSeating)
            {
                int[] ticket_composition = new int[3]; // Array of 3 ints, representing how many child, adult and student tickets make up the booked_tickets
                int ticket_type = 0; // Int representing what type of ticket is selected (0 - child, 1 - adult, 2 - student)
                double total; // A double representing a tally price of all the tickets

                Write(
                    booked_tickets.Contains((sr * 9) + sc) ? "0" : (
                        (this[sc, sr]) ? "X" : "."
                    ),
                    booked_tickets.Contains((sr * 9) + sc) ? ConsoleColor.Blue : (
                        (this[sc, sr]) ? ConsoleColor.Red : ConsoleColor.DarkGray
                    ),
                    2 + (sc * 2), 4 + sr
                );

                Write($"YOU HAVE SELECTED ( {booked_tickets.Count()} ) SEATS", ConsoleColor.White, 0, 10);
                Write("----------------------------------", ConsoleColor.DarkGray, 0, 11);
                Write("£X.XX Child Tickets          ", ConsoleColor.White, 0, 12);
                Write("3.50", ConsoleColor.Yellow, 1, 12);
                Write("£X.XX Adult Tickets          ", ConsoleColor.White, 0, 13);
                Write("7.00", ConsoleColor.Yellow, 1, 13);
                Write("£X.XX Student Tickets        ", ConsoleColor.White, 0, 14);
                Write("4.00", ConsoleColor.Yellow, 1, 14);
                Write("----------------------------------", ConsoleColor.DarkGray, 0, 15);
                Write("YOUR TOTAL: £X.XX", ConsoleColor.White, 0, 16);
                Write("0.00", ConsoleColor.Yellow, 13, 16);

                Write("Please select ticket types   ", ConsoleColor.Red, 0, 18);
                Write("Distribute tickets using ARROW KEYS", ConsoleColor.White, 0, 19);
                Write("ESCAPE to cancel, ENTER to confirm", ConsoleColor.White, 0, 20);

                for (int i = 0; i < 3; i++)
                    Write($" < {ticket_composition[i]} > ", ConsoleColor.DarkGray, 22, 12 + i);
                Write($"<< {ticket_composition[ticket_type]} >>", ConsoleColor.White, 22, ticket_type + 12);

                while (true)
                {
                    ConsoleKey key = Console.ReadKey(true).Key;

                    Write($" < {ticket_composition[ticket_type]} > ", ConsoleColor.DarkGray, 22, ticket_type + 12);

                    if (key == ConsoleKey.Enter)
                    {
                        if ((filmData & 0b11) == 3 && (ticket_composition[0] > 0 || ticket_composition[2] > 0))
                            // IF age rating is 18 AND any ticket that isnt adult is booked ISSUE warning
                            Write($"This is an adult-only viewing, you cannot book child or student tickets", ConsoleColor.Red, 0, 18, true);
                        else if (ticket_composition.Sum() < booked_tickets.Count())
                            // IF not all seats have been given a ticket ISSUE warning
                            Write($"You must pay for all tickets ({booked_tickets.Count() - ticket_composition.Sum()} left)", ConsoleColor.Red, 0, 18, true);
                        else
                        {
                            Write($" < {ticket_composition[ticket_type]} > ", ConsoleColor.DarkGray, 22, ticket_type + 12); // Re-color count (just design i like)
                            break;
                        }
                    }
                    else if (key == ConsoleKey.Escape) // ESCAPE -> Go back 1 stage
                    {
                        Flush(9); return false;
                    }
                    else if (key == ConsoleKey.UpArrow && ticket_type > 0) ticket_type--; // UP ARROW -> change ticket type (student -> child -> adult)
                    else if (key == ConsoleKey.DownArrow && ticket_type < 2) ticket_type++; // DOWN ARROW -> change ticket type (child -> adult -> student)
                    else if (key == ConsoleKey.LeftArrow && ticket_composition[ticket_type] > 0)
                        // LEFT ARROW -> MINUS 1 ticket from current ticket type if tickets arent all distributed
                        ticket_composition[ticket_type]--;
                    else if (key == ConsoleKey.RightArrow && ticket_composition.Sum() < booked_tickets.Count())
                        // RIGHT ARROW -> ADDS 1 ticket from current ticket type if tickets arent all distributed
                        ticket_composition[ticket_type]++;

                    // Calculate the total price of all the tickets
                    total = ticket_composition[0] * 3.5d + ticket_composition[1] * 7.0d + ticket_composition[2] * 4.0d;

                    Write($"<< {ticket_composition[ticket_type]} >>", ConsoleColor.White, 22, ticket_type + 12);
                    Write(
                        String.Format("{0:0.00}", total).PadRight(10, ' '), ConsoleColor.Yellow, 13, 16
                    );
                }

                long bookingOverwrites = 0; // A long representing only YOUR booked seats

                foreach (int seatPosition in booked_tickets)
                    bookingOverwrites |= 0b10L << seatPosition + 1; // Construct the long from an array of ints 

                filmData |= bookingOverwrites; // Add overwrites to actual data

                Program.Cache.Add(this, bookingOverwrites);
                Program.Cache.Save();



                Flush(17);
                Write("----------------------------------------------------------------", ConsoleColor.DarkGray, 0, 18);
                Write("Aquinas Multiplex: Delivering your favourite films since 1980!", ConsoleColor.Blue, 0, 19);
                Write($"FILM: {name}", ConsoleColor.Yellow, 0, 20);
                Write($"DATE: {0}", ConsoleColor.Yellow, 0, 21);

                Write("Enjoy the film!", ConsoleColor.Green, 0, 22);
                Write("----------------------------------------------------------------", ConsoleColor.DarkGray, 0, 23);

                for (int i = 5; i > 0; i--)
                {
                    Write($"Returning to home in {i} seconds . . . ", ConsoleColor.DarkGray, 16, 22);
                    System.Threading.Thread.Sleep(1000); // Waits 1000 ms
                }
                return true;
            }


            public int GetAvailability()
                => 45 - Convert.ToString((filmData & ((1L << 47) - 4)) >> 2, 2).Count((char c) => c == '1');
            public double GetQuality()
                => (filmData >> 47 & 31);
            public string GetStarRating()
                => ("".PadLeft((int)(5 * GetQuality() / 32.0d), '★') + "☆☆☆☆☆.").Remove(5);
            public string GetAgeRating()
            {
                switch (filmData & 0b11) // filmData & 0b11 will get the last 2 bits of the filmData, which represent the age rating 0 - 3 or U - 18
                {
                    case 0:
                        return "U";
                    case 1:
                        return "12A";
                    case 2:
                        return "15";
                    case 3:
                        return "18";
                    default:
                        return "";
                }
            }
            public ConsoleColor GetAgeRatingColor()
            {
                switch (filmData & 0b11) // filmData & 0b11 will get the last 2 bits of the filmData, which represent the age rating 0 - 3 or U - 18
                {
                    case 0:
                        return ConsoleColor.Green;
                    case 1:
                        return ConsoleColor.Yellow;
                    case 2:
                        return ConsoleColor.Magenta;
                    case 3:
                        return ConsoleColor.Red;
                    default:
                        return ConsoleColor.White;
                }
            }


            public bool IsAvailable()
                => (filmData & ((1L << 47) - 4)) >> 2 != (1L << 45) - 1;

        };

        static Film SearchFilm()
        // A function that will take user input for a name and date, and generate a screening based off of just that
        {
            Flush();
            Console.CursorVisible = true; // Show the little blinking cursor that indicates you are typing
            string name;

            Write("SEARCH: ", ConsoleColor.Gray, 0, 0);
            Console.SetCursorPosition(8, 0); // Places cursor in front of the "SEARCH: " line

            do
            {
                name = (Console.ReadLine() ?? "").ToLower(); // Ensures all alphabetical characters are in lowercase

                if (name.Length == 0)
                    Write("You must enter the name of a film!", ConsoleColor.Red, 0, 1, true);
                else if (name.Count(char.IsLetterOrDigit) == 0)
                    Write("The film name must have atleast 1 alphanumerical character!", ConsoleColor.Red, 0, 1, true);
                else
                    break;
            } while (true);

            Console.CursorVisible = false;
            if (name.Length > 64)
                name.Remove(64); // Ensure the name entered is less than 64 characters, to not break any GUI

            DateTime now = DateTime.Now; // Saves calling DateTime.Now a bunch
            DateTime date = now;
            ConsoleKey key;

            // Setup inital values
            int selectionType = 0;
            int selectionValue = date.Day;
            int selectionMin = 1, selectionMax = DateTime.DaysInMonth(date.Year, date.Month);

            Write($"ENTER DATE: XX/XX/XX", ConsoleColor.Gray, 0, 1, true);

            do
            {
                Write($"{date.Day,2}/{date.Month,2}/{date.Year,4}".Replace(' ', '0'), ConsoleColor.DarkGray, 12, 1);
                // The {var, int} represents padding so {1, 2} would become " 1" and {31, 2} would become "31"
                // Then we just replace all spaces with 0, to make it look like "28/09/2023" instead of "28/ 9/2023"

                switch (selectionType)
                {
                    case 0: // Selecting Day
                        selectionMin = now.Day;
                        selectionMax = now.AddDays(7).Day; // Maximum is what the day would be in a weeks time
                        selectionValue = Clamp(date.Day, selectionMin, selectionMax);
                        Write($"{selectionValue,2}".Replace(' ', '0'), ConsoleColor.White, 12, 1); break;

                    case 1: // Selecting Month
                        selectionMin = now.Month;
                        selectionMax = now.AddDays(7).Month;
                        selectionValue = Clamp(date.Month, selectionMin, selectionMax);
                        Write($"{selectionValue,2}".Replace(' ', '0'), ConsoleColor.White, 15, 1); break;

                    case 2: // Selecting Year
                        selectionMin = now.Year;
                        selectionMax = now.AddDays(7).Year;
                        selectionValue = Clamp(date.Year, selectionMin, selectionMax);
                        Write($"{selectionValue,4}".Replace(' ', '0'), ConsoleColor.White, 18, 1); break;
                }

                key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.Enter)
                    break;
                else if (key == ConsoleKey.Escape)
                    return new Film("", 0); // Return an "empty" film
                else if ((key == ConsoleKey.RightArrow && selectionType < 2) || (key == ConsoleKey.LeftArrow && selectionType > 0))
                { // IF RIGHTARROW is pressed and SELECTION is not at end or LEFTARROW is pressed and SELECTION is not at start THEN move SELECTION up or down
                    selectionType += key == ConsoleKey.RightArrow ? 1 : -1;

                    switch (selectionType)
                    {
                        case 0:
                            selectionValue = date.Day; break;
                        case 1:
                            selectionValue = date.Month; break;
                        case 2:
                            selectionValue = date.Year; break;
                    }
                }
                else if ((key == ConsoleKey.UpArrow && selectionValue < selectionMax) || (key == ConsoleKey.DownArrow && selectionValue > selectionMin))
                { // IF UPARROW is pressed or DOWNARROW is pressed THEN add or subract 1 FROM SELECTION
                    selectionValue += key == ConsoleKey.UpArrow ? 1 : -1;

                    switch (selectionType)
                    {
                        // We must create new datetime objects as they are immutable and DateTime.AddDays and others arent reliable
                        case 0:
                            date = new DateTime(date.Year, date.Month, selectionValue); break;
                        case 1:
                            date = new DateTime(date.Year, selectionValue, date.Day); break;
                        case 2:
                            date = new DateTime(selectionValue, date.Month, date.Day); break;
                    }
                }
                else
                    continue;
            } while (true);

            return new Film(name, date);

        }
        static void Main(string[] args)
        {
            const int RECOMMENDED_LIMIT = 5;  // Constant representing how many films should be shown at home page (will break if above 9)
            Console.OutputEncoding = Encoding.Unicode; // Allow for more symbols to be shown
            Console.CursorVisible = false; // Remove blinking cursor (cuz i like it dat way)

            Random prng = new Random(DateTime.Now.Day); // Provide different seed once per day so the recommended films change every day (if there are enough to pick from)
            Film film; // Current selected film
            Film[] library = new Film[] // Array of pre-defined films
            {
                new Film("rush", 3659202005296886), // Film(NAME, FILMDATA)
                new Film("how i live", 3377701360956074),
                new Film("thor: the dark world", -961972275),
                new Film("filth", -767804001),
                new Film("planes", 2111062325434456),
            };

            List<int> selections = Enumerable.Range(0, library.Count()).ToList(); // Create a list of indexes the size of the library 

            for (int i = 0; i < (RECOMMENDED_LIMIT - library.Count()); i++) // Remove element from selections until only RECOMMENED_LIMIT amount of elements are left
                selections.RemoveAt(prng.Next(0, selections.Count()));

            int selection;

            while (true)
            {
                Console.Clear();
                Write("Welcome to Aquinas Multiplex! We are showing the following today:", ConsoleColor.Yellow, 0, 0);
                Write("1.\n2.\n3.\n4.\n5.", ConsoleColor.DarkGray, 0, 2);
                for (int i = 0; i < RECOMMENDED_LIMIT; i++)
                {
                    film = library[selections[i]];
                    Write(ToTitle(film.name), ConsoleColor.White, 3, 2 + i);
                    Write(film.GetAgeRating(), film.GetAgeRatingColor(), film.name.Length + 4, 2 + i);
                }
                Write("Press 1 - 5 to make a selection\nPress ENTER to search for a film via the Babel Film Library", ConsoleColor.Gray, 0, 3 + RECOMMENDED_LIMIT);
                Write("WARNING: May cause untold timeline causality, aswell as other time travel multiverse jargon", ConsoleColor.DarkRed, 0, 5 + RECOMMENDED_LIMIT);

                selection = (int)Console.ReadKey(true).Key;

                if (selection > 48 && selection < 58 && selection - 48 <= RECOMMENDED_LIMIT)
                    // IF a number was pressed AND shown on homepage THEN begin booking process
                    library[selection - 49].SelectSeating(true);
                else if (selection == 13) // IF key ENTER is pressed THEN begin film searching process
                {
                    film = SearchFilm();
                    if (!(film.name == ""))
                        film.SelectSeating();
                }
            }
        }
    }
}