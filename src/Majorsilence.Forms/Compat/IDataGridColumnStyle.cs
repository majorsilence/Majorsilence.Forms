namespace Majorsilence.Forms
{
    /// <summary>
    ///  Defines the common members implemented by column style classes used by <see cref="DataGrid"/>.
    /// </summary>
    public interface IDataGridColumnStyle
    {
        /// <summary>
        ///  Gets or sets the header text displayed for this column.
        /// </summary>
        string HeaderText { get; set; }

        /// <summary>
        ///  Gets or sets the name of the data member mapped to this column.
        /// </summary>
        string MappingName { get; set; }
    }
}