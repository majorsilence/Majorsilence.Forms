Imports System.Drawing
Imports System.Windows.Forms

Namespace WinFormsCompatDemoVb
    Partial Class Form1
        Inherits Form

        Private WithEvents greetButton As Button
        Private nameBox As TextBox
        Private greeting As Label

        Private Sub InitializeComponent()
            Me.nameBox = New TextBox()
            Me.greetButton = New Button()
            Me.greeting = New Label()
            Me.SuspendLayout()
            '
            'nameBox
            '
            Me.nameBox.Location = New Point(12, 12)
            Me.nameBox.Name = "nameBox"
            Me.nameBox.Size = New Size(200, 23)
            '
            'greetButton
            '
            Me.greetButton.Location = New Point(218, 11)
            Me.greetButton.Name = "greetButton"
            Me.greetButton.Size = New Size(75, 25)
            Me.greetButton.Text = "Greet"
            '
            'greeting
            '
            Me.greeting.AutoSize = True
            Me.greeting.Font = New Font("Arial", 12.0F, FontStyle.Bold)
            Me.greeting.Location = New Point(12, 52)
            Me.greeting.Name = "greeting"
            '
            'Form1
            '
            Me.ClientSize = New Size(310, 100)
            Me.Controls.Add(Me.nameBox)
            Me.Controls.Add(Me.greetButton)
            Me.Controls.Add(Me.greeting)
            Me.Name = "Form1"
            Me.Text = "Visual Basic on Majorsilence.Forms"
            Me.ResumeLayout(False)
            Me.PerformLayout()
        End Sub
    End Class
End Namespace
