// IDriver.cs - 驾驶员顶级接口
namespace MoonRover.Driver
{
    public interface IDriver
    {
        // 获取当前驾驶员决定的方向盘输入值 (-1 到 1)
        // 用于 TrajectoryPredictor 画预测线
        float GetSteeringInput();

        // 开启或剥夺该驾驶员的控制权
        void EnableDriver(bool enable);
    }
}