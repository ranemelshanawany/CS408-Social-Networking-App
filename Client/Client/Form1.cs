using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public partial class Form1 : Form
    {
        bool terminating = false;
        bool connected = false;
        Socket clientSocket;
        string myName; //stores name of current client

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }

        private void button_connect_Click(object sender, EventArgs e)
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            string IP = textBox_ip.Text;

            int portNum;
            if (Int32.TryParse(textBox_port.Text, out portNum)) //if port number correct
            {
                try
                {
                    //send username
                    string message = txtName.Text;
                    if (message != "")
                    {

                        //connect to server
                        clientSocket.Connect(IP, portNum);
                        button_connect.Enabled = false;
                        button_discon.Enabled = true;
                        textBox_message.Enabled = true;
                        button_send.Enabled = true;
                        connected = true;
                        textBoxAdd.Enabled = true;
                        btnAdd.Enabled = true;
                        buttonAccept.Enabled = true;
                        buttonReject.Enabled = true;
                        buttonList.Enabled = true;
                        button1.Enabled = true;
                        textBoxRemove.Enabled = true;
                        btnToFriend.Enabled = true;


                        Byte[] buffer = new Byte[64];
                        buffer = Encoding.Default.GetBytes(message);
                        clientSocket.Send(buffer);

                        //recieve whether connection is allowed
                        Byte[] buffer2 = new Byte[64];
                        clientSocket.Receive(buffer2);
                        string incomingMessage = Encoding.Default.GetString(buffer2);
                        incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));

                        //if connection not allowed, disconnect
                        if (incomingMessage != "stay connected")
                        {
                            button_connect.Enabled = true;
                            button_discon.Enabled = false;
                            textBox_message.Enabled = false;
                            button_send.Enabled = false;
                            textBoxAdd.Enabled = false;
                            btnAdd.Enabled = false;
                            buttonAccept.Enabled = false;
                            buttonReject.Enabled = false;
                            buttonList.Enabled = false;
                            btnToFriend.Enabled = false;
                            terminating = true;
                            logs.AppendText("Not allowed to connect \n");
                            clientSocket.Close();
                        }
                        //else inform user connection successful
                        else
                        {
                            logs.AppendText("Connected to the server!\n");
                            myName = message;
                        }

                        Thread receiveThread = new Thread(Receive);
                        receiveThread.Start();
                    }
                    else
                    {
                        logs.AppendText("Enter username!\n");
                    }

                }
                catch
                {
                    logs.AppendText("Could not connect to the server!\n");
                }
            }
            else
            {
                logs.AppendText("Check the input info\n");
            }

        }

        private void Receive()
        {
            while (connected)
            {
                try
                {
                    //recieve messages from server
                    Byte[] buffer = new Byte[64];
                    clientSocket.Receive(buffer);
                    //decode
                    string incomingMessage = Encoding.Default.GetString(buffer);
                    incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));

                    //if its a friend request
                    if (incomingMessage.Length >= 7 && incomingMessage.Substring(0, 7) == "Request")
                    {
                        listView1.Items.Add(incomingMessage.Substring(7));
                        logs.AppendText("Friend request recieved from " + incomingMessage.Substring(7) + "\n");
                    }
                    //if its a request not answered yet
                    else if (incomingMessage.Length >= 10 && incomingMessage.Substring(0, 10) == "OldRequest")
                    {
                        listView1.Items.Add(incomingMessage.Substring(10));  //add to pending friend requests menu
                    }
                    //if its a name of a person who accepted your request
                    else if (incomingMessage.Length >= 5 && incomingMessage.Substring(0, 5) == "Added")
                    {
                        logs.AppendText(incomingMessage.Substring(5) + " accepted your friend request.\n");
                        logs.AppendText(incomingMessage.Substring(5) + " added to friends.\n");

                    }
                    //if its name of a person who rejected you
                    else if (incomingMessage.Length > 8 && incomingMessage.Substring(0, 8) == "Rejected")
                    {
                        logs.AppendText(incomingMessage.Substring(8) + " rejected your friend request.\n");
                    }
                    else {
                        //display message
                        logs.AppendText(incomingMessage);
                    }
                }
                catch
                {
                    //if server disconnects
                    if (!terminating)
                    {
                        logs.AppendText("The server has disconnected\n");
                        button_connect.Enabled = true;
                        button_discon.Enabled = false;
                        textBox_message.Enabled = false;
                        button_send.Enabled = false;
                        textBoxAdd.Enabled = false;
                        btnAdd.Enabled = false;
                        buttonAccept.Enabled = false;
                        buttonReject.Enabled = false;
                        buttonList.Enabled = false;
                        button1.Enabled = false;
                        textBoxRemove.Enabled = false;
                        btnToFriend.Enabled = false;
                        listView1.Items.Clear();
                    }
                    connected = false;
                    clientSocket.Close();
                }

            }
            if (!connected)
            {
                button_connect.Enabled = true;
                button_discon.Enabled = false;
                textBox_message.Enabled = false;
                button_send.Enabled = false;
                textBoxAdd.Enabled = false;
                btnAdd.Enabled = false;
                buttonAccept.Enabled = false;
                buttonReject.Enabled = false;
                buttonList.Enabled = false;
                button1.Enabled = false;
                textBoxRemove.Enabled = false;
                btnToFriend.Enabled = false;
                listView1.Items.Clear();
                clientSocket.Close();
            }
        }

        //if exit button clicked
        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            connected = false;
            terminating = true;
            Environment.Exit(0);
        }

        //SEND MESSAGES TO ALL CONNECTED
        private void button_send_Click(object sender, EventArgs e)
        {
            //read message to send
            string message = textBox_message.Text;

            //send message to server
            if (message != "" && message.Length <= 64)
            {
                Byte[] buffer = new Byte[64];
                buffer = Encoding.Default.GetBytes(message);
                clientSocket.Send(buffer);
                logs.AppendText("Sent: " + message + "\n");
            }
            else
                logs.AppendText("Message too long.\n");
        }

        //DISCONNECTING 
        private void button_discon_Click(object sender, EventArgs e)
        {
            button_connect.Enabled = true;
            button_discon.Enabled = false;
            textBox_message.Enabled = false;
            button_send.Enabled = false;
            connected = false;
            terminating = true;
            textBoxAdd.Enabled = false;
            btnAdd.Enabled = false;
            buttonAccept.Enabled = false;
            buttonReject.Enabled = false;
            buttonList.Enabled = false;
            button1.Enabled = false;
            textBoxRemove.Enabled = false;
            btnToFriend.Enabled = false;
            listView1.Items.Clear();
            logs.AppendText("Disconnected \n");
            clientSocket.Close();
        }

        //SEND FRIEND REQUEST
        private void btnAdd_Click(object sender, EventArgs e)
        {
            string name = textBoxAdd.Text;
            if (name == "") 
                logs.AppendText("No name entered! Enter name.\n");
            else if (name == myName) 
                logs.AppendText("Can't send friend request to yourself.\n");
            else
            {
                //send name to server, server should check if it exists and then forward it to friend or tell us its not allowed
                string sendMsg = "Request" + name;
                if (sendMsg.Length <= 64)
                {
                    Byte[] buffer = new Byte[64];
                    buffer = Encoding.Default.GetBytes(sendMsg);
                    clientSocket.Send(buffer);
                }
                else
                    logs.AppendText("Name too long.\n");
            }
        }

        //ACCEPT FRIEND REQUEST
        private void buttonAccept_Click(object sender, EventArgs e)
        {
            try
            {
                //send name selected to server with keyword "Added", remove from listview
                String addedFriend = listView1.SelectedItems[0].Text;
                listView1.SelectedItems[0].Remove();

                logs.AppendText(addedFriend + " added to friends\n");
                addedFriend = "Added" + addedFriend;

                
                Byte[] buffer = new Byte[64];
                buffer = Encoding.Default.GetBytes(addedFriend);
                clientSocket.Send(buffer);
                
            }
            catch
            {
                logs.AppendText("Nothing selected to accept/reject.\n");
            }
        }

        //REJECT FRIEND REQUEST
        private void buttonReject_Click(object sender, EventArgs e)
        {
            try
            {
                //remove from listview
                String rejectedFriend = listView1.SelectedItems[0].Text;
                listView1.SelectedItems[0].Remove();

                //add keyword "Rejected"
                logs.AppendText(rejectedFriend + " rejected\n");
                rejectedFriend = "Rejected" + rejectedFriend;

                //send to server
                Byte[] buffer = new Byte[64];
                buffer = Encoding.Default.GetBytes(rejectedFriend);
                clientSocket.Send(buffer);
            }
            catch
            {
                logs.AppendText("Nothing selected to accept/reject.\n");
            }
        }

        //LIST CURRENT FRIENDS 
        private void buttonList_Click(object sender, EventArgs e)
        {
            //send keyword to list friends to server
            string listFriends = "List Friends";
            Byte[] buffer = new Byte[64];
            buffer = Encoding.Default.GetBytes(listFriends);
            clientSocket.Send(buffer);
        }
        
        //CLEAR LOGS
        private void btnClear_Click(object sender, EventArgs e)
        {
            logs.Clear();
        }

        //REMOVE FRIEND
        private void button1_Click(object sender, EventArgs e)
        {
            string name = textBoxRemove.Text;
            if (name == "")
                logs.AppendText("No name entered! Enter name.\n");
            else if (name == myName)
                logs.AppendText("Can't send friend request to yourself.\n");
            else
            {
                //send name to server, server should check if it exists and then forward it to friend or tell us its not allowed
                string sendMsg = "Remove" + name;
                if (sendMsg.Length <= 64)
                {
                    Byte[] buffer = new Byte[64];
                    buffer = Encoding.Default.GetBytes(sendMsg);
                    clientSocket.Send(buffer);
                }
                else
                    logs.AppendText("Name too long.\n");
            }
        }

        //SEND TO FRIENDS ONLY
        private void btnToFriend_Click(object sender, EventArgs e)
        {
            string message = "ToFriends" + textBox_message.Text;

            //send message to server
            if (message != "" && message.Length <= 64)
            {
                Byte[] buffer = new Byte[64];
                buffer = Encoding.Default.GetBytes(message);
                clientSocket.Send(buffer);
                logs.AppendText("Sent: " + message.Substring(9) + "\n");
            }
            else
                logs.AppendText("Message too long.\n");
        }
        
    }
}
