namespace Brisa.Core;
public sealed class NetworkMeter
{
    long received, sent;
    DateTimeOffset? at;
    public (double Down, double Up) Sample(long receiveTotal, long sendTotal, DateTimeOffset now)
    {
        double seconds = at.HasValue ? (now - at.Value).TotalSeconds : 0;
        double down = seconds > 0 && receiveTotal >= received ? (receiveTotal - received) / seconds : 0;
        double up = seconds > 0 && sendTotal >= sent ? (sendTotal - sent) / seconds : 0;
        received = receiveTotal; sent = sendTotal; at = now;
        return (down, up);
    }
}
