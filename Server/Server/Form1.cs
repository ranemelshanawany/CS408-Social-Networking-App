using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Server
{
    public partial class Form1 : Form
    {

        bool terminating = false;
        bool listening = false;

        //in order to store user name with socket
        struct socketnName
        {
            public Socket sock;
            public string Name;
        }

        //stores online users
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        List<socketnName> clients = new List<socketnName>();

        //to store all users info, and forward messages when online
        struct node
        {
            public string name;
            public List<string> requestsPending;  //requests waiting until online to be sent
            public List<string> responsesPending; //responses waiting to be online to be sent
            public List<string> friends;          //current friends
            public List<string> sent;             //people you're waiting for a response from
            public List<string> recieved;         //people waiting for you to respond
            public List<string> removedPending;   //people who removed you from friends
            public List<string> messagePending;   //messages sent from friends while offline
        }
        List<node> users = new List<node>();

        public Form1()
        {
            String path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory.ToString());
            path = path.Substring(0, path.IndexOf("bin")) + "user_db.txt";
            StreamReader file = new System.IO.StreamReader(@path);


            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }

        //when exit button clicked
        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            listening = false;
            terminating = true;
            Environment.Exit(0);
        }

        //LISTENING STARTS
        private void button_listen_Click(object sender, EventArgs e)
        {
            int serverPort;
            //start listening process
            try
            {
                if (Int32.TryParse(textBox_port.Text, out serverPort))
                {
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort);
                    serverSocket.Bind(endPoint);
                    serverSocket.Listen(100);

                    listening = true;
                    button_listen.Enabled = false;

                    Thread acceptThread = new Thread(Accept);
                    acceptThread.Start();

                    logs.AppendText("Started listening on port: " + serverPort + "\n");

                }
                else
                {
                    logs.AppendText("Please check port number \n");
                }
            }
            catch
            {
                logs.AppendText("Please check port number \n");
            }
        }

        //ACCEPT OR REJECT CLIENT
        private void Accept()
        {
            while (listening)
            {
                try
                {
                    socketnName newClient;
                    newClient.sock = serverSocket.Accept();

                    //recieve name of user
                    Byte[] buffer = new Byte[64];
                    newClient.sock.Receive(buffer);
                    string name = Encoding.Default.GetString(buffer);
                    name = name.Substring(0, name.IndexOf("\0"));

                    //add client to list
                    newClient.Name = name;
                    clients.Add(newClient);

                    //open user database
                    String path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory.ToString());
                    path = path.Substring(0, path.IndexOf("bin")) + "user_db.txt";
                    StreamReader file = new System.IO.StreamReader(@path);

                    //to track whether client allowed to connect or not
                    bool allow = false;

                    //check if name in database
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        if (line == name)
                        {
                            allow = true;
                            break;
                        }
                    }

                    //check if name is already connected
                    foreach (socketnName client in clients)
                    {
                        if (client.sock != newClient.sock)
                        {
                            if (client.Name == name)
                            {
                                allow = false;
                                break;
                            }
                        }
                    }


                    //if exists in database and not already connected, connect
                    if (allow)
                    {
                        logs.AppendText(name + " is connected.\n");
                        Byte[] buffer2 = new Byte[64];
                        string success = "stay connected";
                        buffer2 = Encoding.Default.GetBytes(success);
                        newClient.sock.Send(buffer2);
                        if (!users.Exists(x => x.name == name))  //if user not registered
                        {
                            //add them to users list
                            node addThis;
                            addThis.name = name;
                            addThis.requestsPending = new List<string>();
                            addThis.responsesPending = new List<string>();
                            addThis.friends = new List<string>();
                            addThis.sent = new List<string>();
                            addThis.recieved = new List<string>();
                            addThis.removedPending = new List<string>();
                            addThis.messagePending = new List<string>();
                            users.Add(addThis);
                        }

                        //if user regstered
                        if (users.Exists(x => x.name == name))
                        {
                            string message;
                            //check if there are friend requests still not answered to
                            foreach (string element in users.Find(x => x.name == name).recieved)
                            {
                                message = "OldRequest" + element;
                                Byte[] buffer3 = new Byte[64];
                                buffer3 = Encoding.Default.GetBytes(message);
                                newClient.sock.Send(buffer3); //send to client
                            }

                            //check if while offine someone sent them a friend request
                            foreach (string element in users.Find(x => x.name == name).requestsPending)
                            {
                                message = element;
                                Byte[] buffer3 = new Byte[64];
                                buffer3 = Encoding.Default.GetBytes(message);
                                newClient.sock.Send(buffer3);                  //send to client
                                users.Find(x => x.name == name).recieved.Add(message.Substring(7));  //add to delivered friend requests
                                logs.AppendText("Friend request delivered to " + name + " from " + message.Substring(7) + "\n");
                            }

                            //check if someone responded to friend request while offine
                            foreach (string element in users.Find(x => x.name == name).responsesPending)
                            {
                                message = element;
                                Byte[] buffer3 = new Byte[64];
                                buffer3 = Encoding.Default.GetBytes(message);
                                newClient.sock.Send(buffer3);
                            }

                            //check if someone unfriended you
                            foreach (string element in users.Find(x => x.name == name).removedPending)
                            {
                                message = element;
                                Byte[] buffer3 = new Byte[64];
                                buffer3 = Encoding.Default.GetBytes(message);
                                newClient.sock.Send(buffer3);
                            }

                            //check if anyone sent a message
                            foreach (string element in users.Find(x => x.name == name).messagePending)
                            {
                                message = element;
                                Byte[] buffer3 = new Byte[64];
                                buffer3 = Encoding.Default.GetBytes(message);
                                newClient.sock.Send(buffer3);
                            }

                            //delete pending requests and responses since theyre all delivered
                            int length = users.Find(x => x.name == name).requestsPending.Count;
                            for (int i = 0; i < length; i++)
                            {
                                users.Find(x => x.name == name).requestsPending.RemoveAt(0);
                            }
                            length = users.Find(x => x.name == name).responsesPending.Count;
                            for (int i = 0; i < length; i++)
                            {
                                users.Find(x => x.name == name).responsesPending.RemoveAt(0);
                            }

                            //delete all pending messages and removal notif
                            length = users.Find(x => x.name == name).messagePending.Count;
                            for (int i = 0; i < length; i++)
                            {
                                users.Find(x => x.name == name).messagePending.RemoveAt(0);
                            }
                            length = users.Find(x => x.name == name).removedPending.Count;
                            for (int i = 0; i < length; i++)
                            {
                                users.Find(x => x.name == name).removedPending.RemoveAt(0);
                            }
                        }
                        

                        Thread receiveThread = new Thread(() => Receive(name));
                        receiveThread.Start();
                    }
                    //delete and disconnect, signal client to disconnect 
                    else
                    {
                        Byte[] buffer2 = new Byte[64];
                        string success = "disconnect yo self";
                        buffer2 = Encoding.Default.GetBytes(success);
                        newClient.sock.Send(buffer2);
                        logs.AppendText(newClient.Name + " not allowed to connect " + "\n");
                        newClient.sock.Close();
                        clients.Remove(newClient);
                    }

                }
                catch
                {
                    if (terminating)
                    {
                        listening = false;
                    }
                    else
                    {
                        logs.AppendText("The socket stopped working.\n");

                    }

                }
            }
        }

        //RECIEVE MESSAGES FROM CLIENT AND FORWARD TO CONNECTED CLIENTS
        private void Receive(string name)
        {
            Socket thisClientSock = clients[clients.Count() - 1].sock;
            socketnName thisClient = clients[clients.Count() - 1];

            bool connected = true;

            while (connected && !terminating)
            {
                try
                {
                    //recieve message from client and decode
                    Byte[] buffer = new Byte[64];
                    thisClientSock.Receive(buffer);
                    string incomingMessage = Encoding.Default.GetString(buffer);
                    incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));


                    string clientName = clients.Find(x => x.sock == thisClientSock).Name;

                    //IF A FRIEND REQUEST
                    if (incomingMessage.Length >= 7 && incomingMessage.Substring(0, 7) == "Request")
                    {
                        //open database
                        String path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory.ToString());
                        path = path.Substring(0, path.IndexOf("bin")) + "user_db.txt";
                        StreamReader file = new System.IO.StreamReader(@path);

                        string requestName = incomingMessage.Substring(7);

                        //check if name in database
                        string result = "Friend not in database\n";
                        bool inDatabase = false;
                        string line;
                        while ((line = file.ReadLine()) != null)
                        {
                            if (line == requestName)  //if in database
                            {

                                result = "Friend request sent to " + requestName + "\n";
                                if (users.Find(x => x.name == clientName).sent.Exists(x => x == requestName) || users.Find(x => x.name == clientName).recieved.Exists(x => x == requestName))
                                //if there is a pending friend request
                                {
                                    result = "Friend request sent/recieved before\n";
                                }
                                else if ((users.Find(x => x.name == clientName).friends.Exists(x => x == requestName)))
                                //if already in friends
                                {
                                    result = requestName + " is already a friend\n";
                                }
                                else
                                {
                                    logs.AppendText(name + " sent friend request to " + requestName + "\n");
                                    inDatabase = true;
                                }
                                break;
                            }
                        }

                        //SEND WHETHER FRIEND REQUEST SENT OR NOT
                        bool online = clients.Exists(x => x.Name == requestName);
                        Byte[] buffer2 = new Byte[64];
                        buffer2 = Encoding.Default.GetBytes(result);
                        clients.Find(x => x.Name == name).sock.Send(buffer2);

                        //BUFFER OF CLIENTNAME TO SEND TO INVITEE
                        string clientNameBuffer = "Request" + clientName;
                        Byte[] buffer3 = new Byte[64];
                        buffer3 = Encoding.Default.GetBytes(clientNameBuffer);


                        if (online && inDatabase)  //if invitee online
                        {
                            Socket requestTo = clients.Find(x => x.Name == requestName).sock;   //find them
                            requestTo.Send(buffer3);  //send friend request
                            users.Find(x => x.name == requestName).recieved.Add(clientName);
                            users.Find(x => x.name == clientName).sent.Add(requestName);
                        }
                        else if (inDatabase)  //if not in online
                        {
                            if (!users.Exists(x => x.name == requestName))  //if invitee not registered in users
                            {
                                //register and add pending request
                                node addThis;
                                addThis.name = requestName;
                                addThis.requestsPending = new List<string>();
                                addThis.responsesPending = new List<string>();
                                addThis.friends = new List<string>();
                                addThis.requestsPending.Add("Request" + clientName);
                                addThis.sent = new List<string>();
                                addThis.recieved = new List<string>();
                                addThis.removedPending = new List<string>();
                                addThis.messagePending = new List<string>();
                                users.Add(addThis);
                            }
                            else //if registered, add to pending requests
                            {
                                users.Find(x => x.name == requestName).requestsPending.Add("Request" + clientName);
                            }
                            users.Find(x => x.name == clientName).sent.Add(requestName);
                        }

                    }
                    //IF REMOVING A FRIEND
                    else if (incomingMessage.Length >= 6 && incomingMessage.Substring(0, 6) == "Remove")
                    {
                        string unfriendedName = incomingMessage.Substring(6);
                        if (!users.Find(x => x.name == clientName).friends.Contains(unfriendedName))
                        {
                            string message = unfriendedName + " not in friends\n";
                            Byte[] buffer2 = new Byte[64];
                            buffer2 = Encoding.Default.GetBytes(message);
                            clients.Find(x => x.Name == clientName).sock.Send(buffer2);
                        }
                        else
                        {
                            //remove from friends of both 
                            users.Find(x => x.name == clientName).friends.Remove(unfriendedName);
                            users.Find(x => x.name == unfriendedName).friends.Remove(clientName);
                            logs.AppendText(clientName + " removed " + unfriendedName + " from friends\n");

                            //send a message to client that they were removed
                            string message = unfriendedName + " unfriended.\n";
                            Byte[] buffer2 = new Byte[64];
                            buffer2 = Encoding.Default.GetBytes(message);
                            clients.Find(x => x.Name == clientName).sock.Send(buffer2);

                            message = clientName + " unfriended you\n";
                            buffer2 = Encoding.Default.GetBytes(message);

                            //if unfriended is online message them else store in pendingRemoved
                            if (clients.Exists(x => x.Name == unfriendedName))
                            {
                                clients.Find(x => x.Name == unfriendedName).sock.Send(buffer2); //if online
                            }
                            else
                            {
                                users.Find(x => x.name == unfriendedName).removedPending.Add(message);  //if offline
                            }
                        }
                    }
                    //IF AN ACCEPTANCE OF FRIEND REQUEST
                    else if (incomingMessage.Length > 5 && incomingMessage.Substring(0, 5) == "Added")
                    {
                        string friendName = incomingMessage.Substring(5);
                        users.Find(x => x.name == clientName).recieved.Remove(friendName);//remove from friend requests not responded to 
                        users.Find(x => x.name == friendName).sent.Remove(clientName);

                        users.Find(x => x.name == friendName).friends.Add(clientName);  //add to friends of both
                        users.Find(x => x.name == clientName).friends.Add(friendName);
                        logs.AppendText(clientName + " accepted " + friendName + "'s friend request\n");

                        //inform inviter of acceptance
                        if (clients.Exists(x => x.Name == friendName))
                        {
                            string message = clientName + " accepted your friend request\n";
                            buffer = Encoding.Default.GetBytes(message);
                            clients.Find(x => x.Name == friendName).sock.Send(buffer);  //if online
                        }
                        else
                        {
                            users.Find(x => x.name == friendName).responsesPending.Add(clientName + " accepted your friend request\n");  //if offline
                        }
                    }
                    //IF A REJECTANCE OF FRIEND REQUEST 
                    else if (incomingMessage.Length > 8 && incomingMessage.Substring(0, 8) == "Rejected")
                    {
                        string rejectedName = incomingMessage.Substring(8);
                        users.Find(x => x.name == clientName).recieved.Remove(rejectedName);  //remove from not responded to
                        users.Find(x => x.name == rejectedName).sent.Remove(clientName);

                        logs.AppendText(clientName + " rejected " + rejectedName + "'s friend request\n");

                        //inform inviter 
                        if (clients.Exists(x => x.Name == rejectedName))
                        {
                            clients.Find(x => x.Name == rejectedName).sock.Send(buffer); //if online
                        }
                        else
                        {
                            users.Find(x => x.name == rejectedName).responsesPending.Add(clientName + " rejected your friend request\n");  //if offline
                        }
                    }
                    //IF REQUEST TO LIST FRIENDS
                    else if (incomingMessage == "List Friends")
                    {
                        string message;
                        int length = users.Find(x => x.name == clientName).friends.Count;
                        if (length != 0)  //if they have friends
                        {
                            message = "List of friends: \n";
                            Byte[] buffer2 = new Byte[64];
                            buffer2 = Encoding.Default.GetBytes(message);
                            clients.Find(x => x.Name == clientName).sock.Send(buffer2);

                            //send all entries in client's friends list
                            for (int i = 0; i < length; i++)
                            {
                                message = users.Find(x => x.name == clientName).friends[i] + "\n";
                                Byte[] buffer3 = new Byte[64];
                                buffer3 = Encoding.Default.GetBytes(message);
                                clients.Find(x => x.Name == clientName).sock.Send(buffer3);
                            }
                        }
                        else
                        {
                            message = "No friends\n";
                            Byte[] buffer3 = new Byte[64];
                            buffer3 = Encoding.Default.GetBytes(message);
                            clients.Find(x => x.Name == clientName).sock.Send(buffer3);
                        }
                    }
                    //IF A MESSAGE TO BE SEND TO FRIENDS
                    else if (incomingMessage.Length > 9 && incomingMessage.Substring(0, 9) == "ToFriends")
                    {
                        string outgoing = name + ": " + incomingMessage.Substring(9) + "\n";
                        logs.AppendText(outgoing);

                        //send to all friends
                        Byte[] buffer2 = new Byte[64];
                        buffer2 = Encoding.Default.GetBytes(outgoing);
                        //for each of thier friends
                        foreach (string friendName in users.Find(x => x.name == clientName).friends)
                        {
                            //send message or add to pending messages
                            if (clients.Exists(x => x.Name == friendName))
                            {
                                clients.Find(x => x.Name == friendName).sock.Send(buffer2); //if online
                            }
                            else
                            {
                                users.Find(x => x.name == friendName).messagePending.Add(outgoing);  //if offline
                            }
                        }

                        if (users.Find(x => x.name == clientName).friends.Count == 0)
                        {
                            outgoing = "No friends to send message to\n";
                            buffer2 = Encoding.Default.GetBytes(outgoing);
                            clients.Find(x => x.Name == clientName).sock.Send(buffer2);
                        }
                    }
                    //MESSAGE TO ALL CLIENTS
                    else 
                    {
                        incomingMessage = name + ": " + incomingMessage + "\n";
                        logs.AppendText(incomingMessage);

                        //send to all other connected client
                        Byte[] buffer2 = new Byte[64];
                        buffer2 = Encoding.Default.GetBytes(incomingMessage);
                        foreach (socketnName client in clients)
                        {
                            if (client.sock != thisClientSock)
                                client.sock.Send(buffer2);
                        }
                    }
                }
                catch
                {
                    //if client disconnects
                    if (!terminating)
                    {
                        logs.AppendText(name + " has disconnected\n");
                    }
                    thisClientSock.Close();
                    clients.Remove(thisClient);
                    connected = false;
                }
            }
        }
    }
}
