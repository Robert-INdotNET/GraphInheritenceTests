using Detached.Annotations;
using System.ComponentModel.DataAnnotations;

namespace GraphInheritenceTests.ComplexModels
{
    public class OrganizationNotes : NotesBase
    {
        // Back-references don't work
        //[Required]
        //public int OrganizationId { get; set; }

        //[Aggregation]
        //[Required]
        //public OrganizationBase Organization { get; set; }
    }
}