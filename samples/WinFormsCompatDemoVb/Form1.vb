Imports System.Windows.Forms

Namespace WinFormsCompatDemoVb
    Public Class Form1

        Public Sub New()
            InitializeComponent()
        End Sub

        Private Sub greetButton_Click(sender As Object, e As EventArgs) Handles greetButton.Click
            greeting.Text = If(String.IsNullOrWhiteSpace(nameBox.Text), "Hello, world", "Hello, " & nameBox.Text)
        End Sub

    End Class
End Namespace
