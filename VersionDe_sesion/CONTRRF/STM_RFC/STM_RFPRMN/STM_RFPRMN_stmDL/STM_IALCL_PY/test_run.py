import sys, os
sys.path.insert(0, r"E:\WoldVirtualP2P3D\VersionDe_sesion\CONTRRF\STM_RFC\STM_RFPRMN\STM_RFPRMN_stmDL\STM_IALCL_PY")
from STM_IALOCAL import IAAllocator

a = IAAllocator()
a.load_model("gpt2")
print("Models:", a.list_models())
r = a.edit_code("print('Hola')\n")
print("Edit lines:", r["lines"])
saved = a.save_code("demo.py", "print('Hola', 'world')\n")
print("Saved:", saved)
