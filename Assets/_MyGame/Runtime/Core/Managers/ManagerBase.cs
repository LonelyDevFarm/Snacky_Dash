using UnityEngine;

namespace SnackDash.Core
{
    /// <summary>
    /// Lớp chung của mọi Manager trên object <c>[Managers]</c> của scene. <see cref="ManagersRoot"/> gọi <see cref="Init"/>
    /// một lần, theo thứ tự cố định, ngay khi scene mở đầu bắt đầu (trước mọi script khác); Manager tự đăng ký vào <see cref="Services"/>.
    /// </summary>
    public abstract class ManagerBase : MonoBehaviour
    {
        public abstract void Init();
    }
}
