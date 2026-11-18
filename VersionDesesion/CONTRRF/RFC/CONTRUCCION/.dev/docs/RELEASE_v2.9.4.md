# Wold Virtual P2P 3D v2.9.4 — Reconexión Automática DAO y Túnel Efímero

Esta versión menor estabiliza la conectividad bidireccional y la experiencia de usuario en la **Isla Única LucIA (Sede DAO)**, resolviendo la pérdida del túnel efímero al retornar a la DAO e implementando un flujo de actualización ultra-ligero.

---

## 🚀 Novedades y Mejoras Principales

### 1. Reconexión Automática y Persistencia de la DAO (`IslaUnicaLucIA.gd`)
* **Persistencia Atómica del Túnel DAO:** `_realizar_conexion_dao()` ahora serializa y publica el estado en `user://tunel_dao_url.json` mediante escritura atómica (`.tmp` → `rename`), permitiendo que `LuciaRecepcion._poll_dao()` detecte la sesión activa de inmediato.
* **Handshake con LucIA Host:** Conexión sincronizada con el servicio modular P2P y registro de ciudadanos.
* **Terminal DAO 3D Interactivo:** Reconexión inmediata por proximidad al aproximarse a la terminal holográfica en la plaza DAO.

### 2. Gestión de Ciclo de Vida Espacial en Teletransporte (`TeleportPanel.gd`)
* **Invalidación Inteligente al Salir (`_invalidar_tunel_dao`):** Al navegar hacia cualquier otra isla del archipiélago, el sistema marca el túnel como liberado (`exito: false`) y resetea el flag de sesión.
* **Auto-conexión al Regresar (`_auto_conectar_dao`):** Al retornar a la isla DAO, el gestor de teletransporte revalida y reabre el túnel efímero automáticamente, copiando el nuevo enlace público al portapapeles sin intervención manual.

### 3. Ahorro Masivo de Tamaño y Eficiencia en Distribución
* **Paquete Delta v2.9.4 (Solo 11 KB):** En lugar de redistribuir binarios completos de 1.41 GB (que acarreaban motores Godot de 86 MB y PCKs duplicados de 1.28 GB), esta actualización se distribuye como un paquete delta modular de apenas **11 KB**, logrando un **ahorro superior al 99.9%** en descarga.
* **Túneles SSH Nativos vs Daemons Externos:** Empleo de túneles SSH efímeros (a través de `localhost.run` / `serveo.net`) utilizando el cliente SSH nativo del sistema operativo, eliminando la necesidad de empaquetar binarios propietarios pesados de túneles (ahorrando entre 30 y 50 MB de dependencias).

---

## 📦 Contenido del Paquete de Actualización (`DAOLCDMWoldVirtualP2P3Dv2.9.4.zip`)
* `CAMBIOS_DAO_RECONEXION.md`: Registro exhaustivo de cambios técnicos.
* `gdscrip/TeleportPanel.gd`: Gestor de teletransporte con hooks de entrada/salida DAO.
* `lucia3D/IslaUnicaLucIA/IslaUnicaLucIA.gd`: Controlador de la Isla Única LucIA con publicación del túnel.

---

## 🔍 Documentación Relacionada
* **Artículo de Gobernanza y DeFi 3D:** Consulte `ARTICULO_DAO_DEFI_METAVERSO.md` para conocer el funcionamiento de la Asamblea 3D, la Bóveda de Tesorería Comunitaria, el Staking de Nodos y el rol de LucIA como Oráculo Autónomo.
* **Informe de Deuda Técnica:** Consulte `DEUDA_TECNICA_v2.9.4.md` o el Issue correspondiente en el repositorio.
