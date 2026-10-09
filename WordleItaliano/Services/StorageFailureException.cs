using System.IO;

namespace WordleItaliano.Services;

public sealed class StorageFailureException : IOException
{
    public StorageFailureException(Exception error) : base(
        "Non è stato possibile leggere o salvare tutti i dati. Wordle deve chiudersi per proteggerli. " +
        "Non cancellare i file: al prossimo avvio verrà tentato il recupero. Se il problema continua, chiedi assistenza.", error) { }
}
