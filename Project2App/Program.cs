using System;
using System.Threading;
using System.Collections;


namespace Project2App
{   
    public class Theater
    {
        private const int tmax = 20; //Maximum Theater Price Ticket Cuts

        private int t = 1; // Counter for the price cuts
        private int currentTicketPrice = 0; // Current Price for the tickets
        private int previousTicketPrice = 0; // Previous Price for the tickets
        private static Random random = new Random();

        private ArrayList processingThreads = new ArrayList();

        public delegate void PriceCutHandler(PriceCutEventArgs e);
        public event PriceCutHandler PriceCut;

        private void PriceCutEvent()
        {
            // To check if there are any subscribers to the event
            if (PriceCut != null)
            {
                Console.WriteLine("EVENT: Performing Price Cut #{0} for {1}", t, Thread.CurrentThread.Name);
                t++;
                PriceCut(new PriceCutEventArgs(Thread.CurrentThread.Name, currentTicketPrice)); // Fire the event
            }
            else
            {
                Console.WriteLine("ERROR: No PriceCut event subscribers");
            }
        }

        private void SetPrice() //set the current ticket price
        {
            Console.WriteLine("PRICING: {0} : Calculating", Thread.CurrentThread.Name);
            currentTicketPrice = PricingModel.GetRates(); //call get rates method to set the price
            Console.WriteLine("PRICING: {0} Price Finalized: {1}",Thread.CurrentThread.Name,currentTicketPrice.ToString("C"));
        }

        private OrderClass RetrieveOrder()
        {
            return (Project2.multiBuffer.getOneCell()); //get an order from the multibuffer cell
        }

        private void ProcessOrder(OrderClass order)
        {
            Console.WriteLine("RECEIVING: Order for {0}", Thread.CurrentThread.Name);
            OrderProcessing processor = new OrderProcessing(order, currentTicketPrice); //process the order and calculate the amount
            Thread processingThread = new Thread(new ThreadStart(processor.ProcessOrder));
            processingThreads.Add(processingThread);                                   //start a new order processing thread
            processingThread.Name = "Processor:" + Thread.CurrentThread.Name;
            processingThread.Start();
        }

        public void Run()
        {
            while (t <= tmax) // Check for tmax price cuts
            {

                SetPrice(); //set the current ticket price

                Console.WriteLine("CHECKING: ({0}) Price Comparison : ({1} to {2})",
                    Thread.CurrentThread.Name,
                    previousTicketPrice.ToString("C"),
                    currentTicketPrice.ToString("C")
                );

                if (currentTicketPrice < previousTicketPrice)
                {
                    PriceCutEvent();  //if PriceCut has been made, fire the event
                }
                previousTicketPrice = currentTicketPrice;  // Set the previous Price to the current Price
                ProcessOrder(RetrieveOrder()); // Retrieve and Process orders from the Multi-Cell buffer
            }

            foreach (Thread order in processingThreads) //waits for any remaining unprocessed threads in the buffer to finish before closing the theatre thread
            {
                while (order.IsAlive) ;
            }

            Console.WriteLine("CLOSING: Theater Thread ({0})", Thread.CurrentThread.Name);
        }
    }
    public class MultiCellBuffer
    {
        private const int n = 2; //Size of the Multi-Cell Buffer
        private const int writeResources = 2;
        private const int readResources = 1;

        int head = 0;
        int tail = 0;
        int numberOfElements = 0; // variable to keep track of buffer position

        OrderClass[] buffer = new OrderClass[n]; // Buffer initialisation

        Semaphore write = new Semaphore(writeResources, writeResources);
        Semaphore read = new Semaphore(readResources, readResources);         //Semaphores to control read/write access

        public OrderClass getOneCell()
        {
            read.WaitOne();
            Console.WriteLine("ENTERED READ: " + Thread.CurrentThread.Name);
            lock (this)
            {
                OrderClass element;
                // Busy Wait until an Element is in the Multi-Cell Buffer
                while (numberOfElements == 0)
                {
                    Console.WriteLine("MONITOR: Read Waiting: {0}", Thread.CurrentThread.Name);
                    Monitor.Wait(this);
                }

                element = buffer[head]; //take element from the buffer

                head = (head + 1) % n;
                numberOfElements--; //empty buffer by one element
                Console.WriteLine("READING: {0} : Multi-Cell Buffer\n\n{1}, Elements: {2}\n",
                    Thread.CurrentThread.Name,element,numberOfElements);
                
                Console.WriteLine("LEAVING READ: {0}", Thread.CurrentThread.Name);
                
                read.Release();
                Monitor.Pulse(this);
                return element;
            }
        }

        public void setOneCell(OrderClass order)
        {
            write.WaitOne();
            Console.WriteLine("ENTERED WRITE: " + Thread.CurrentThread.Name);
            lock (this)
            {
                // Busy Wait until there is a slot available in the Multi-Cell Buffer
                while (numberOfElements == n)
                {
                    Console.WriteLine("MONITOR: Write Waiting {0}", Thread.CurrentThread.Name);
                    Monitor.Wait(this);
                }

                buffer[tail] = order;
                tail = (tail + 1) % n; //add order to the buffer and update the tail

                Console.WriteLine("WRITING: ({0}) Multi-Cell Buffer\n\n{1}, Elements: {2}\n",
                    Thread.CurrentThread.Name,
                    order,
                    numberOfElements
                );

                numberOfElements++; // Increment the number of elements
                Console.WriteLine("LEAVING WRITE: {0}", Thread.CurrentThread.Name);
                write.Release();
                Monitor.Pulse(this);
            }
        }
    }
    public class OrderClass
    {
        private string senderId; // Identity of the sender (i.e. TicketBroker)
        private long cardNo; // An integer that represents a credit card number
        private int quantity; // Represents the number of seats to order
        private int unitPrice; //price of the ticket

        public override string ToString() 
        {
            return "ORDER\n\t{ID: " + SenderId
                + "}\n\t{CARD_NO: " + CardNo
                + "}\n\t{AMOUNT: " + Quantity 
                +"}\n\t{UNIT PRICE: " + UnitPrice + "}";
        }

        public int UnitPrice
        {
            get { return unitPrice; }
            set { unitPrice = value; }
        }

        public string SenderId
        {
            get { return senderId; }
            set { senderId = value; }
        }

        public long CardNo
        {
            get { return cardNo; }
            set { cardNo = value; }
        }

        public int Quantity
        {
            get { return quantity; }
            set { quantity = value; }
        }
    }
    public class OrderProcessing
    {
        private const int TAX = 5;
        private const int LOCATION_CHARGE = 30;

        private OrderClass order;
        private int unitPrice;

        public OrderProcessing(OrderClass order, int unitPrice)
        {
            this.Order = order;
            this.UnitPrice = unitPrice;
        }

        public void ProcessOrder() //processes the order received from the buffer
        {
            if (order != null)
            {

                // Check for a valid credit card number
                if (ValidateCreditCard(order.CardNo))
                {
                    Console.WriteLine("VALIDATION CHECK COMPLETE for: {0} : Credit Card Number Valid", Thread.CurrentThread.Name);
                }
                else
                {
                    Console.WriteLine("VALIDATION CHECK COMPLETE for: {0} : Credit Card Number Not Valid", Thread.CurrentThread.Name);
                    return;
                }

                Console.WriteLine("PROCESSING COMPLETE for: {0} Ticket Broker Order {1}\n\tTOTAL PRICE: {2}",
                    Thread.CurrentThread.Name, order.ToString(),
                    ((order.Quantity * unitPrice) + TAX + LOCATION_CHARGE).ToString("C")
                );
            }
            else
            {
                Console.WriteLine("PROCESSING: {0} : No order received", Thread.CurrentThread.Name);
            }
        }

        private bool ValidateCreditCard(long creditCardNum)
        {
            int length = creditCardNum.ToString().Length; 
            string last4digit = creditCardNum.ToString().Substring(length-4); //convert credit card num to string and extract last 4 digit

            int cardDigits = Int32.Parse(last4digit); //convert last 4 digit string to int

            bool flag = ((length == 16) && (5000 < cardDigits && cardDigits < 7000)); //check if credit card number length is valid and last 4 digits lies b/w 5000 and 7000

            return flag;
        }

        public OrderClass Order
        {
            get { return order; }
            private set { order = value; }
        }

        public int UnitPrice
        {
            get { return unitPrice; }
            set { unitPrice = value; }
        }

    }
    public class PriceCutEventArgs : EventArgs //pass the thread id and price to the ticket brokers.
    {
        private int price;
        private string id;

        public PriceCutEventArgs(string id, int price)
        {
            this.Id = id;
            this.Price = price;
        }

        public string Id
        {
            get { return id; }
            set { id = value; }
        }

        public int Price
        {
            get { return price; }
            private set { price = value; }
        }
    }
    public static class PricingModel
    {
        public static int GetRates()
        {
            Random rand = new Random();
            int rPrice = rand.Next(40, 200); //assign a random price between 40 and 200

            return rPrice;
        }
    }
    public class TicketBroker
    {
        private int quantity = random.Next(20, 38); //random nuumber of seats

        private static bool theaterActive = true; //flag to indicate if theater thread is alive or not
        private static Random random = new Random();
        private bool seatsNeeded = true;
        private bool bulkOrder = true;
        private int unitPrice;

        // Random Credit Cards to Test
        private static readonly long[] cc_Numbers =
        {
            9088373723126644,   
            3612366382365168,   
            374951333742767,    
            3900366099125857,     
            6011354933529823,   
            3553991022586867,    
            1238031289080833102 
        };

        public void Subscribe(Theater Theater)
        {
            Console.WriteLine("SUBSCRIBING to: Price Cut Event");
            Theater.PriceCut += IssueOrder;
        }

        private void CreateOrder()
        {
            Console.WriteLine("CREATING: Order ({0})", Thread.CurrentThread.Name);
            seatsNeeded = false; // Order not required
            OrderClass order = new OrderClass();
            order.Quantity = quantity;
            order.CardNo = cc_Numbers[random.Next(0, cc_Numbers.Length)];
            order.SenderId = Thread.CurrentThread.Name;
            order.UnitPrice = unitPrice;

            Project2.multiBuffer.setOneCell(order);
        }

        public void IssueOrder(PriceCutEventArgs e)
        {
            unitPrice = e.Price;
            if(unitPrice < 120)
            {
                bulkOrder = true;
            }
        }

        public static bool TheaterActive
        {
            get { return TicketBroker.theaterActive; }
            set { TicketBroker.theaterActive = value; }
        }

        public void Run()
        {
            // Continue thread until theater are no longer active
            while (theaterActive)
            {
                // Check if an order needs to be created
                if (seatsNeeded && bulkOrder)
                {
                    CreateOrder();
                }
                else
                {
                    // No orders are needed thread sleeps for some time
                    Console.WriteLine("WAITING: Ticket Broker Thread ({0})", Thread.CurrentThread.Name);
                    Thread.Sleep(1000);
                    seatsNeeded = true;
                }
            }

            Console.WriteLine("CLOSING: Ticket Broker Thread ({0})", Thread.CurrentThread.Name);
        }
    }

    public class Project2
    {
        private const int K = 1; // Number of Theaters
        private const int N = 5; // Number of Ticket Brokers

        private static Thread[] theaterThreads = new Thread[K];
        private static Thread[] ticketBrokerThreads = new Thread[N];
        private static Theater[] Theaters = new Theater[K];

        public static MultiCellBuffer multiBuffer = new MultiCellBuffer();

        static void Main(string[] args)
        {
            // Initialize the Theater
            for (int i = 0; i < K; ++i)
            {
                Theater Theater = new Theater();
                Theaters[i] = Theater;
                theaterThreads[i] = new Thread(Theater.Run);
                theaterThreads[i].Name = "Theater_" + i;
                theaterThreads[i].Start();
                while (!theaterThreads[i].IsAlive) ;
            }

            // Initialize the Ticket Brokers
            for (int i = 0; i < N; ++i)
            {
                TicketBroker TicketBroker = new TicketBroker();

                // Loop through the Theater and Subscribe to the Price Cut event
                for (int j = 0; j < K; ++j)
                {
                    TicketBroker.Subscribe(Theaters[j]);
                }

                ticketBrokerThreads[i] = new Thread(TicketBroker.Run);
                ticketBrokerThreads[i].Name = "TicketBroker_" + i;
                ticketBrokerThreads[i].Start();
                while (!ticketBrokerThreads[i].IsAlive) ;
            }

            // Wait for the theater to perform tmax price cuts
            for (int i = 0; i < K; ++i)
            {
                while (theaterThreads[i].IsAlive) ;
            }

            // Alert the Ticket Brokers that the theater thread is no longer active
            for (int i = 0; i < N; ++i)
            {
                TicketBroker.TheaterActive = false;
            }

            // Wait for the Ticket Broker to close
            for (int i = 0; i < N; ++i)
            {
                while (ticketBrokerThreads[i].IsAlive) ;
            }
            Console.WriteLine("\nProgram Completed!!");
        }
    }
}
