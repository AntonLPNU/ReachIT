using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IForegroundWindowService
{
    ForegroundWindowSnapshot GetCurrent();
}
