namespace Prismedia.Contracts.Opds;

/// <summary>
/// Constants for the OPDS 1.2 Atom catalog surface.
/// </summary>
public static class OpdsProtocol {
    /// <summary>Root path for the OPDS catalog.</summary>
    public const string Prefix = "/opds";

    /// <summary>HTTP Basic authentication challenge for OPDS reader clients.</summary>
    public const string BasicChallenge = "Basic realm=\"Prismedia OPDS\"";

    /// <summary>HTTP Basic authorization scheme name.</summary>
    public const string BasicScheme = "Basic";

    /// <summary>Prismedia generator label used in OPDS feeds.</summary>
    public const string Generator = "Prismedia";

    /// <summary>Atom XML namespace URI.</summary>
    public const string AtomNamespace = "http://www.w3.org/2005/Atom";

    /// <summary>OPDS 1.2 catalog namespace URI.</summary>
    public const string OpdsNamespace = "http://opds-spec.org/2010/catalog";

    /// <summary>Dublin Core Terms namespace URI.</summary>
    public const string DublinCoreTermsNamespace = "http://purl.org/dc/terms/";

    /// <summary>OpenSearch namespace URI.</summary>
    public const string OpenSearchNamespace = "http://a9.com/-/spec/opensearch/1.1/";

    /// <summary>OPDS HTTP content type strings.</summary>
    public static class ContentTypes {
        /// <summary>Navigation feed response content type.</summary>
        public const string NavigationFeed = "application/atom+xml;profile=opds-catalog;kind=navigation";

        /// <summary>Acquisition feed response content type.</summary>
        public const string AcquisitionFeed = "application/atom+xml;profile=opds-catalog;kind=acquisition";

        /// <summary>Generic Atom response content type.</summary>
        public const string Atom = "application/atom+xml";

        /// <summary>OpenSearch descriptor response content type.</summary>
        public const string OpenSearch = "application/opensearchdescription+xml";

        /// <summary>Plain text content type for navigation summaries.</summary>
        public const string Text = "text";
    }

    /// <summary>Atom and OPDS link relations used by catalog feeds.</summary>
    public static class LinkRelations {
        /// <summary>Current document relation.</summary>
        public const string Self = "self";

        /// <summary>Catalog root relation.</summary>
        public const string Start = "start";

        /// <summary>Parent feed relation.</summary>
        public const string Up = "up";

        /// <summary>Search descriptor relation.</summary>
        public const string Search = "search";

        /// <summary>Next page relation.</summary>
        public const string Next = "next";

        /// <summary>Previous page relation.</summary>
        public const string Previous = "previous";

        /// <summary>Navigation subsection relation.</summary>
        public const string Subsection = "subsection";

        /// <summary>OPDS acquisition relation.</summary>
        public const string Acquisition = "http://opds-spec.org/acquisition";

        /// <summary>OPDS full-size image relation.</summary>
        public const string Image = "http://opds-spec.org/image";

        /// <summary>OPDS thumbnail image relation.</summary>
        public const string Thumbnail = "http://opds-spec.org/image/thumbnail";
    }

    /// <summary>OPDS query string parameter names.</summary>
    public static class Query {
        /// <summary>One-based page number.</summary>
        public const string Page = "page";

        /// <summary>Page size.</summary>
        public const string Limit = "limit";

        /// <summary>Search terms.</summary>
        public const string Search = "q";
    }

    /// <summary>OPDS route path helpers.</summary>
    public static class Routes {
        public const string Catalog = Prefix + "/catalog";
        public const string Recent = Prefix + "/recent";
        public const string Libraries = Prefix + "/libraries";
        public const string Authors = Prefix + "/authors";
        public const string Series = Prefix + "/series";
        public const string Collections = Prefix + "/collections";
        public const string Tags = Prefix + "/tags";
        public const string Search = Prefix + "/search";
        public const string OpenSearch = Prefix + "/opensearch.xml";

        public static string Library(Guid id) => $"{Libraries}/{id}";

        public static string Author(Guid id) => $"{Authors}/{id}";

        public static string SeriesItem(Guid id) => $"{Series}/{id}";

        public static string Collection(Guid id) => $"{Collections}/{id}";

        public static string Tag(Guid id) => $"{Tags}/{id}";

        public static string Book(Guid id) => $"{Prefix}/books/{id}";

        public static string BookCover(Guid id) => $"{Book(id)}/cover";

        public static string BookDownload(Guid id) => $"{Book(id)}/download";
    }

    /// <summary>OPDS feed urn helpers.</summary>
    public static class Urns {
        public const string Root = "urn:prismedia:opds:root";
        public const string Catalog = "urn:prismedia:opds:catalog";
        public const string Recent = "urn:prismedia:opds:recent";
        public const string Libraries = "urn:prismedia:opds:libraries";
        public const string Authors = "urn:prismedia:opds:authors";
        public const string Series = "urn:prismedia:opds:series";
        public const string Collections = "urn:prismedia:opds:collections";
        public const string Tags = "urn:prismedia:opds:tags";
        public const string Search = "urn:prismedia:opds:search";

        public static string Library(Guid id) => $"urn:prismedia:opds:library:{id}";

        public static string Author(Guid id) => $"urn:prismedia:opds:author:{id}";

        public static string SeriesItem(Guid id) => $"urn:prismedia:opds:series:{id}";

        public static string Collection(Guid id) => $"urn:prismedia:opds:collection:{id}";

        public static string Tag(Guid id) => $"urn:prismedia:opds:tag:{id}";

        public static string Book(Guid id) => $"urn:prismedia:book:{id}";
    }
}
