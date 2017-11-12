using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BlackJack.BotGames
{
    public class BlackJack
    {
        static Player[] players = new Player[2];
        static int pointer = 0;
        private static DateTime LastPlay;

        public class PlayingCard
        {
            public string Suit;
            public int Value;
            public int Points;

            // Alternate Constructor with 2 parameters - Int for Suit, Int for Value
            // We use this in the generateDeck() function
            public PlayingCard(int s, int v)
            {
                Value = v; // Sets the Value of the card to the value of v (The second argument)
                switch (s) // Case statement based on the value of s
                {
                    case 1: // If s == 1, then set the Suit to Clubs
                        Suit = "♣";
                        break;
                    case 2: // If s == 2, then set the Suit to Diamonds
                        Suit = "♦";
                        break;
                    case 3: // If s == 3, then set the Suit to Hearts
                        Suit = "♥";
                        break;
                    case 4: // If s == 4, then set the Suit to Spades
                        Suit = "♠";
                        break;
                }

                if (Value > 10)
                {
                    Points = 10;
                }
                else if (Value == 1) // If it's an ace
                {
                    Points = 11; // Set the points to 11
                }
                else
                {
                    Points = Value;
                }
            }
        }

        public class Player
        {
            public PlayingCard[] hand;
            public int cardsInHand;
            public int points;
            public string name;

            public Player()
            {
                hand = new PlayingCard[5];
                cardsInHand = 0;
                points = 0;
            }

        }



        // Generates the deck of 52 cards
        public static PlayingCard[] generateDeck()
        {
            PlayingCard[] deck = new PlayingCard[52]; // Declares an array of PlayingCards with a size of 52
            int counter = 0; // Tells us where to save the next value into the array

            // Nested for loop to generate all 52 cards - 4 possible suits with 13 possible values each
            for (int suit = 1; suit < 5; suit++) // Loop through the 4 possible suits
            {
                for (int value = 1; value < 14; value++) // Loop through the 13 possible values
                {
                    deck[counter] = new PlayingCard(suit, value); // Generate new card and store it in the deck
                    counter++; // Increment the counter
                }
            }

            return deck; // Returns the completed deck
        }

        // Procedure to shuffle the deck of cards
        public static void shuffleDeck(ref PlayingCard[] deck)
        {
            Random rnd = new Random(); // Creates new Random object
            PlayingCard temp; // Creates a variable for temporarily storing a PlayingCard
            int num; // Creates an integer variable for storing the randomly generated numbers

            for (int i = 0; i < deck.Length; i++) // Loop through each index in the array
            {
                num = rnd.Next(0, deck.Length); // Generate random num between 0 & 51

                // Swap the contents of deck[i] and deck[num]
                temp = deck[i];
                deck[i] = deck[num];
                deck[num] = temp;
            }

            // As deck is passed by reference, we do not need to return it to the main program
            // The changes will have automatically been applied to the deck in the main program
        }

        public static void outputCard(PlayingCard card)
        {
            switch (card.Value) // Case statement based on the value of card
            {
                // Case for 1 - "The Ace of ..."
                case 1:
                    Console.WriteLine("The Ace of {0}", card.Suit);
                    break;

                // Case for 11 - "The Jack of ..."
                case 11:
                    Console.WriteLine("The Jack of {0}", card.Suit);
                    break;

                // Case for 12 - "The Queen of ..."
                case 12:
                    Console.WriteLine("The Queen of {0}", card.Suit);
                    break;

                // Case for 13 - "The King of ..."
                case 13:
                    Console.WriteLine("The King of {0}", card.Suit);
                    break;
                // Case for everything else 
                default:
                    Console.WriteLine("The {0} of {1}", card.Value, card.Suit);
                    break;
            }
        }

        // Output the details of a card using symbols - eg/ A♠
        // Used to output the player's hand on the same line (See outputHand procedure below)
        public static void outputCardSymbol(PlayingCard card)
        {
            switch (card.Value) // Case statement based on the value of card
            {
                // Case for 1 - "The Ace of ..."
                case 1:
                    Console.Write("A{0} ", card.Suit);
                    break;

                // Case for 11 - "The Jack of ..."
                case 11:
                    Console.Write("J{0} ", card.Suit);
                    break;

                // Case for 12 - "The Queen of ..."
                case 12:
                    Console.Write("Q{0} ", card.Suit);
                    break;

                // Case for 13 - "The King of ..."
                case 13:
                    Console.Write("K{0} ", card.Suit);
                    break;
                // Case for everything else 
                default:
                    Console.Write("{0}{1} ", card.Value, card.Suit);
                    break;
            }
        }

        // Outputs all of the cards in a player's hand along with their point total
        public static void outputHand(Player player)
        {
            // Print "Current Hand: "
            Console.Write("Current Hand: ");
            // Loop through all cards in hand
            for (int i = 0; i < player.cardsInHand; i++)
            {
                outputCardSymbol(player.hand[i]);
            }
            Console.WriteLine("Current points: {0}.", player.points);
        }

        public static void drawCard(PlayingCard[] deck, ref Player player)
        {
            PlayingCard nextCard = deck[pointer];

            // Add the next card in the deck to the player's hand
            if (player.cardsInHand < 5)
            {
                player.hand[player.cardsInHand] = nextCard;

                // Increment the number of cards in the player's hand
                player.cardsInHand++;

                // Add the point value of the new card to the player's total
                player.points += nextCard.Points;

                // Output the details of the card
                //outputCard(nextCard);

                // Increment the pointer
                pointer++;
            }
        }

        // Check if the player has exceeded 21 points
        // Output the player's point total
        public static bool checkPoints(Player player)
        {
            // Output the player's point total
            //Console.WriteLine("Current Points: {0}", player.points);

            // Check if the player is bust
            if (player.points > 21)
            {
                Console.WriteLine("Bust!");
                return false;
            }

            // Return true if the player is still alive
            return true;
        }

        // Compare the player & the dealer
        public static void calculateWinner(Player player, Player dealer)
        {
            // Player wins if... 
            if (dealer.points > 21 || player.cardsInHand == 5 && dealer.cardsInHand != 5)
            {
                Console.WriteLine("{0} Wins!", player.name);
            }

            // The game ends in a draw if... 
            else if (dealer.points == player.points)
            {
                Console.WriteLine("Draw!");
            }
            // Otherwise, the dealer has won
            else if (dealer.points == 21 && player.points != 21 || player.cardsInHand < 5)
            {
                Console.WriteLine("{0} wins", dealer.points);
            }
            else if (player.cardsInHand == 5 && dealer.cardsInHand == 5)
            {
                if (player.points > dealer.points)
                {
                    Console.WriteLine("{0} wins! 5 card trick: higher than dealers. ", player.name);
                }

                else if (player.points == dealer.points)
                {
                    Console.WriteLine("It's a draw! 5 card trick: same! ");
                }

                Console.WriteLine("{0} wins! 5 card trick: less than dealers. ", dealer.name);
            }
        }

        // Checks if the player has any aces with a point value of 11 (high)
        // If the player is about to go bust, change the ace to a point value of 1 (low)
        // Then update the player's score
        public static void checkAces(ref Player player)
        {
            bool changed = false; // Flags up if we've changed an ace already
            if (player.points > 21)
            {
                for (int i = 0; i < player.cardsInHand; i++)
                {
                    if (player.hand[i].Points == 11 && changed == false) // If it's a high ace
                    {
                        player.hand[i].Points = 1; // Change it to a low ace
                        player.points -= 10; // Take 10 away from player's points
                        changed = true;
                    }
                }
            }

        }

        public static void onePlayer()
        {
            string playAgain = "Undefined";
            do
            {
                // Generate the deck of cards & shuffle it
                PlayingCard[] deck = generateDeck();
                shuffleDeck(ref deck);

                // Create the two player objects
                Player player = new Player();
                Console.Write("Please enter your name: ");
                player.name = Console.ReadLine();

                Player dealer = new Player();
                dealer.name = "Dealer";

                // Draw the first two cards for the Player
                drawCard(deck, ref player);
                drawCard(deck, ref player);


                checkAces(ref player); // Call checkAces to see if we can stop player going bust
                outputHand(player);
                checkPoints(player); // Output the player's point total
                bool alive = true;

                string choice = "Undefined";

                while (alive == true && choice != "STICK")
                {
                    Console.Write("Hit or Stick? ");
                    choice = Console.ReadLine().ToUpper();
                    if (choice == "HIT") // If the user asks to hit then...
                    {
                        drawCard(deck, ref player);

                        // If player still has a valid point total, alive will remain true
                        // If the player is now bust, alive will become false and the loop will exit
                        checkAces(ref player); // Call checkAces to see if we can stop player going bust
                        outputHand(player);
                        alive = checkPoints(player);
                    }
                }
                // If the player isn't bust, it's time for the dealer's turn
                if (alive == true)
                {
                    bool dealerAlive = true;

                    Console.WriteLine();
                    Console.WriteLine("*** Dealer's Turn ***");
                    drawCard(deck, ref dealer);
                    drawCard(deck, ref dealer);

                    checkAces(ref dealer); // Call checkAces to see if we can stop dealer going bust
                    outputHand(dealer);
                    checkPoints(dealer);

                    while (dealerAlive == true)
                    {
                        Console.WriteLine("Press Enter to Continue");
                        Console.ReadLine();

                        // Draw the dealer's next card and check if they are still alive
                        drawCard(deck, ref dealer);

                        checkAces(ref dealer); // Call checkAces to see if we can stop dealer going bust
                        outputHand(dealer);
                        dealerAlive = checkPoints(dealer);
                    }
                }

                // Calculate & output the winner
                calculateWinner(player, dealer);

                Console.Write("Do you want to play again? Y/N ");
                playAgain = Console.ReadLine().ToUpper();
            } while (playAgain == "Y");
        }
    }
}