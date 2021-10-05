using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Eleon.Modding;

namespace EmpyrionModdingFramework
{
  public class RequestManager
  {
    private readonly ModGameAPI modAPI;

    public RequestManager(in ModGameAPI refModApi)
    {
      modAPI = refModApi;
    }

    private static int nextSeqNr = new Random().Next(1025, ushort.MaxValue);
    
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource<object>> taskTracker = new ConcurrentDictionary<ushort, TaskCompletionSource<object>>();

    public async Task<object> SendGameRequest(CmdId cmdID, object data)
    {
      TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
      ushort seqNr = AddTaskCompletionSource(tcs);
      modAPI.Console_Write($"Источник выполнения задач TaskCompletionSource создан для {cmdID} с номером seqNr: {seqNr}");
      modAPI.Game_Request(cmdID, seqNr, data);
      return await tcs.Task;
    }


    private ushort AddTaskCompletionSource(TaskCompletionSource<object> tcs)
    {
      if (nextSeqNr == ushort.MaxValue)
      {
        Interlocked.Exchange(ref nextSeqNr, 1025);
      }

      ushort newSequenceNumber = (ushort)Interlocked.Increment(ref nextSeqNr);

      while (!taskTracker.TryAdd(newSequenceNumber, tcs))
      {
        newSequenceNumber = (ushort)Interlocked.Increment(ref nextSeqNr);
      }

      return newSequenceNumber;
    }

    public bool HandleRequestResponse(CmdId eventId, ushort seqNr, object data)
    {
      if (!taskTracker.TryRemove(seqNr, out TaskCompletionSource<object> taskCompletionSource))
      {
        modAPI.Console_Write($"Источник выполнения задач TaskCompletionSource недоступен для seqNr: {seqNr}");
        return false;
      }
        
      if (eventId == CmdId.Event_Error && data is ErrorInfo eInfo)
      {
        taskCompletionSource.TrySetException(new Exception(eInfo.errorType.ToString()));
        modAPI.Console_Write($"Запрос с seqNr: {seqNr} возвращается с ошибкой события Event_Error, устанавливающей исключение для источника задач TaskCompletionSource.");
        return true;
      }
      else
      {
        try
        {
          taskCompletionSource.TrySetResult(data);
          modAPI.Console_Write($"Запрос с seqNr: {seqNr} завершено, установив результат в источник задач TaskCompletionSource.");
          return true;
        }
        catch (Exception error)
        {
          taskCompletionSource.TrySetException(error);
          modAPI.Console_Write($"Неизвестное исключение для запроса: {seqNr}, установка исключения для источника задач TaskCompletionSource.");
          return false;
        }
      }
    }
  }
}
