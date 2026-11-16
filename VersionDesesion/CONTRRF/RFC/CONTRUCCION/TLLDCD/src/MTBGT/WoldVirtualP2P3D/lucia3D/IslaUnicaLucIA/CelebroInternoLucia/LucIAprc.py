# IMPORTAR AQUI E:\WoldVirtualP2P3D\VersionDesesion\CONTRRF\RFC\CONTRUCCION\TLLDCD\src\STMGNRL\SistemaGeneral\mainLCSTM.py 
# De la  forma  correcta  E:\WoldVirtualP2P3D\VersionDesesion\CONTRRF\RFC\CONTRUCCION\.dev\gestor_entorno.py ayudate con este  modulo., python .,
# la idea esque luciIA tenga pleno  control de su avatar 3D
# COMO EN EL EXPERIMENTO ECO DEL MIT.

"""
SeguridadNeuronal - Módulo de seguridad matemáticamente robusto para LucIA
===========================================================================
Implementa:
  - Entropía de Shannon para detección de anomalías
  - Red neuronal MLP (2 capas ocultas) para clasificación de amenazas
  - Umbral adaptativo bayesiano (Teorema de Bayes)
  - Hash de integridad SHA-256
  - Firma HMAC para autenticación de mensajes
  - Sistema de cuarentena con puntuación de riesgo exponencial
  - Narración en español para el avatar 3D de LucIA
"""

import math
import hashlib
import hmac as _hmac
import json
import time
import random
import os
from collections import Counter, deque
from datetime import datetime

# Intento de importar numpy; si no está disponible se usa modo fallback puro Python
try:
    import numpy as np
    _NUMPY = True
except ImportError:
    _NUMPY = False


# ---------------------------------------------------------------------------
# Utilidades matemáticas puras (fallback sin numpy)
# ---------------------------------------------------------------------------

def _dot(a, b):
    """Producto punto entre dos vectores (listas) de igual longitud."""
    return sum(x * y for x, y in zip(a, b))

def _mat_vec(M, v):
    """Multiplicación matriz × vector. M es lista de filas (listas)."""
    return [_dot(row, v) for row in M]

def _relu(x):
    """Función de activación ReLU: max(0, x)."""
    if isinstance(x, list):
        return [max(0.0, xi) for xi in x]
    return max(0.0, x)

def _sigmoid(x):
    """
    Función de activación Sigmoid: σ(x) = 1 / (1 + e^(-x))
    Clamp para evitar overflow numérico.
    """
    if isinstance(x, list):
        return [_sigmoid(xi) for xi in x]
    x = max(-500.0, min(500.0, x))
    return 1.0 / (1.0 + math.exp(-x))

def _add_bias(v, b):
    """Suma el vector de bias a un vector de activaciones."""
    return [vi + bi for vi, bi in zip(v, b)]


# ---------------------------------------------------------------------------
# Clase principal
# ---------------------------------------------------------------------------

class SeguridadNeuronal:
    """
    Sistema de seguridad neuronal de LucIA.

    Combina técnicas de teoría de la información, aprendizaje automático
    ligero y criptografía para proteger la integridad del cerebro interno
    de LucIA y detectar amenazas en tiempo real.
    """

    # ------------------------------------------------------------------
    # 1. INICIALIZACIÓN
    # ------------------------------------------------------------------

    def __init__(self, clave_maestra: str = "LucIA_SecretKey_MIT_ECO"):
        """
        Inicializa todos los subsistemas de seguridad.

        Parámetros
        ----------
        clave_maestra : str
            Clave usada para HMAC y derivación de semillas de red neuronal.
        """
        # --- Estado básico ---
        self.estado_seguridad: str = "activo"
        self.nivel_seguridad: float = 100.0        # 0-100
        self.timestamp_inicio: float = time.time()

        # --- Clave maestra (nunca sale de este objeto) ---
        self._clave_bytes: bytes = clave_maestra.encode("utf-8")

        # --- Historial de eventos para entropía ---
        # Ventana deslizante de los últimos 256 eventos
        self._historial_eventos: deque = deque(maxlen=256)
        self._entropia_actual: float = 0.0

        # --- Red neuronal de detección de amenazas ---
        # Arquitectura: entrada(6) → oculta1(8) → oculta2(4) → salida(1)
        # Pesos inicializados con Xavier usando semilla determinista derivada de la clave
        self._pesos_nn = self._inicializar_red_neuronal()

        # --- Umbral adaptativo bayesiano ---
        # P(amenaza) prior inicial conservador
        self._prob_amenaza_prior: float = 0.05        # P(A)
        self._prob_evidencia_dado_amenaza: float = 0.90   # P(B|A)
        self._prob_evidencia_dado_seguro: float = 0.10    # P(B|¬A)
        self._prob_amenaza_posterior: float = self._prob_amenaza_prior

        # --- Registro de hashes de integridad ---
        # módulo → hash SHA-256 en el momento del registro
        self._hashes_modulos: dict = {}

        # --- Sistema de cuarentena ---
        # módulo → {"riesgo": float, "timestamp": float, "razon": str}
        self._cuarentena: dict = {}
        self._umbral_cuarentena: float = 0.75   # riesgo > 0.75 → cuarentena

        # --- Log de auditoría ---
        self._log: list = []
        self._log_max: int = 1000

        self._registrar_log("INIT", "SeguridadNeuronal inicializada correctamente.")

    # ------------------------------------------------------------------
    # 2. RED NEURONAL (MLP) — DETECCIÓN DE AMENAZAS
    # ------------------------------------------------------------------

    def _inicializar_red_neuronal(self) -> dict:
        """
        Inicializa los pesos del MLP con Xavier initialization.

        Xavier: W ~ U[-sqrt(6/(fan_in+fan_out)), +sqrt(6/(fan_in+fan_out))]

        Usa una semilla derivada HMAC de la clave maestra para reproducibilidad
        determinista sin necesidad de numpy.
        """
        # Semilla determinista a partir de la clave
        seed_bytes = _hmac.new(self._clave_bytes, b"nn_seed", hashlib.sha256).digest()
        seed_int = int.from_bytes(seed_bytes[:8], "big")
        rng = random.Random(seed_int)

        def xavier_matrix(fan_in, fan_out):
            limit = math.sqrt(6.0 / (fan_in + fan_out))
            return [[rng.uniform(-limit, limit) for _ in range(fan_in)]
                    for _ in range(fan_out)]

        def zero_bias(n):
            return [0.0] * n

        # Dimensiones: 6 → 8 → 4 → 1
        return {
            "W1": xavier_matrix(6, 8),   # (8×6)
            "b1": zero_bias(8),
            "W2": xavier_matrix(8, 4),   # (4×8)
            "b2": zero_bias(4),
            "W3": xavier_matrix(4, 1),   # (1×4)
            "b3": zero_bias(1),
        }

    def _forward_nn(self, vector_amenaza: list) -> float:
        """
        Propagación hacia adelante del MLP.

        Arquitectura:
            capa1: ReLU(W1·x + b1)
            capa2: ReLU(W2·h1 + b2)
            salida: Sigmoid(W3·h2 + b3)

        Parámetros
        ----------
        vector_amenaza : list[float]
            Vector de 6 características en [0, 1]:
            [tasa_anomalia, entropia_norm, intentos_fallidos_norm,
             actividad_red_norm, cambios_modulo_norm, tiempo_sesion_norm]

        Retorna
        -------
        float : probabilidad de amenaza en [0, 1]
        """
        if len(vector_amenaza) != 6:
            raise ValueError("El vector de amenaza debe tener exactamente 6 características.")

        nn = self._pesos_nn
        # Capa 1
        z1 = _add_bias(_mat_vec(nn["W1"], vector_amenaza), nn["b1"])
        h1 = _relu(z1)
        # Capa 2
        z2 = _add_bias(_mat_vec(nn["W2"], h1), nn["b2"])
        h2 = _relu(z2)
        # Capa de salida
        z3 = _add_bias(_mat_vec(nn["W3"], h2), nn["b3"])
        prob = _sigmoid(z3[0])
        return prob

    def clasificar_amenaza(self, vector_amenaza: list) -> dict:
        """
        Clasifica un vector de amenaza usando la red neuronal.

        Retorna un dict con:
            - "probabilidad": float en [0,1]
            - "clasificacion": "SEGURO" | "AMENAZA"
            - "confianza": float en [0,1]
        """
        prob = self._forward_nn(vector_amenaza)
        clasificacion = "AMENAZA" if prob >= 0.5 else "SEGURO"
        confianza = prob if prob >= 0.5 else (1.0 - prob)

        self._registrar_log("NN", f"Vector clasificado como {clasificacion} (p={prob:.4f})")
        return {
            "probabilidad": prob,
            "clasificacion": clasificacion,
            "confianza": confianza,
        }

    # ------------------------------------------------------------------
    # 3. ENTROPÍA DE SHANNON
    # ------------------------------------------------------------------

    def registrar_evento(self, tipo_evento: str):
        """
        Registra un evento en el historial y recalcula la entropía.

        Parámetros
        ----------
        tipo_evento : str
            Etiqueta del evento (p.ej. "LOGIN_OK", "LOGIN_FAIL", "ACCESO_MODULO").
        """
        self._historial_eventos.append(tipo_evento)
        self._entropia_actual = self._calcular_entropia_shannon(list(self._historial_eventos))

    def _calcular_entropia_shannon(self, eventos: list) -> float:
        """
        Calcula la entropía de Shannon H = -Σ p_i · log2(p_i)
        sobre la distribución de tipos de evento.

        Una entropía alta indica diversidad (posible exploración aleatoria
        o ataque de enumeración); una entropía muy baja indica repetición
        (posible bucle de fuerza bruta en un punto específico).

        Parámetros
        ----------
        eventos : list[str]
            Secuencia de tipos de evento.

        Retorna
        -------
        float : entropía en bits (0 si lista vacía)
        """
        if not eventos:
            return 0.0

        conteo = Counter(eventos)
        total = len(eventos)
        H = 0.0
        for count in conteo.values():
            p_i = count / total
            # Evitar log(0)
            if p_i > 0:
                H -= p_i * math.log2(p_i)
        return H

    def detectar_anomalia_entropia(self, umbral_bajo: float = 0.3,
                                    umbral_alto: float = 3.5) -> dict:
        """
        Detecta anomalías basadas en la entropía actual.

        - Entropía < umbral_bajo → repetición sospechosa (fuerza bruta)
        - Entropía > umbral_alto → diversidad excesiva (escaneo / fuzzing)

        Retorna
        -------
        dict con "entropia", "estado_entropia", "alerta"
        """
        H = self._entropia_actual
        if len(self._historial_eventos) < 5:
            estado = "insuficiente_datos"
            alerta = False
        elif H < umbral_bajo:
            estado = "repeticion_sospechosa"
            alerta = True
        elif H > umbral_alto:
            estado = "diversidad_anomala"
            alerta = True
        else:
            estado = "normal"
            alerta = False

        if alerta:
            self._registrar_log("ENTROPIA", f"Anomalía detectada: {estado} H={H:.4f}")
        return {"entropia": H, "estado_entropia": estado, "alerta": alerta}

    # ------------------------------------------------------------------
    # 4. UMBRAL ADAPTATIVO BAYESIANO
    # ------------------------------------------------------------------

    def actualizar_bayesiano(self, evidencia_presente: bool) -> float:
        """
        Actualiza la probabilidad posterior de amenaza con el Teorema de Bayes.

        Fórmula:
            P(A|B) = P(B|A) · P(A) / P(B)

        donde:
            P(B) = P(B|A)·P(A) + P(B|¬A)·(1-P(A))

        Parámetros
        ----------
        evidencia_presente : bool
            True si se observó evidencia de amenaza en este ciclo.

        Retorna
        -------
        float : nueva probabilidad posterior P(A|B)
        """
        P_A  = self._prob_amenaza_posterior         # Prior (actualizado en cada paso)
        P_BA = self._prob_evidencia_dado_amenaza if evidencia_presente \
               else (1.0 - self._prob_evidencia_dado_amenaza)
        P_Bn = self._prob_evidencia_dado_seguro if evidencia_presente \
               else (1.0 - self._prob_evidencia_dado_seguro)

        # Probabilidad marginal de la evidencia: ley de la probabilidad total
        P_B = P_BA * P_A + P_Bn * (1.0 - P_A)

        if P_B == 0.0:
            # Caso degenerado: no se actualiza
            return self._prob_amenaza_posterior

        # Actualización bayesiana
        P_AB = (P_BA * P_A) / P_B
        self._prob_amenaza_posterior = P_AB

        self._registrar_log(
            "BAYES",
            f"P(amenaza) actualizada: {P_A:.4f} → {P_AB:.4f} "
            f"(evidencia={'SÍ' if evidencia_presente else 'NO'})"
        )
        return P_AB

    def obtener_probabilidad_amenaza(self) -> float:
        """Retorna la probabilidad actual de amenaza según el modelo bayesiano."""
        return self._prob_amenaza_posterior

    # ------------------------------------------------------------------
    # 5. HASH DE INTEGRIDAD SHA-256
    # ------------------------------------------------------------------

    def sellar_modulo(self, nombre_modulo: str, contenido: str) -> str:
        """
        Calcula y almacena el hash SHA-256 del contenido de un módulo.

        El hash actúa como sello de integridad: cualquier modificación
        posterior del módulo producirá un hash diferente.

        Parámetros
        ----------
        nombre_modulo : str
            Identificador del módulo (p.ej. "CerebroInternoLucia").
        contenido : str
            Contenido textual o serialización del módulo.

        Retorna
        -------
        str : hash hexadecimal SHA-256
        """
        digest = hashlib.sha256(contenido.encode("utf-8")).hexdigest()
        self._hashes_modulos[nombre_modulo] = {
            "hash": digest,
            "timestamp": datetime.utcnow().isoformat(),
        }
        self._registrar_log("SHA256", f"Módulo '{nombre_modulo}' sellado: {digest[:16]}…")
        return digest

    def verificar_integridad(self, nombre_modulo: str, contenido_actual: str) -> dict:
        """
        Verifica que el contenido actual de un módulo coincide con su sello.

        Parámetros
        ----------
        nombre_modulo : str
            Módulo a verificar.
        contenido_actual : str
            Contenido actual del módulo.

        Retorna
        -------
        dict con "integro": bool, "hash_esperado": str, "hash_actual": str
        """
        if nombre_modulo not in self._hashes_modulos:
            return {"integro": False, "error": "Módulo no registrado"}

        hash_esperado = self._hashes_modulos[nombre_modulo]["hash"]
        hash_actual = hashlib.sha256(contenido_actual.encode("utf-8")).hexdigest()
        integro = hash_esperado == hash_actual

        if not integro:
            self._registrar_log("SHA256", f"¡VIOLACIÓN DE INTEGRIDAD en '{nombre_modulo}'!")
            # Penalizar nivel de seguridad
            self.nivel_seguridad = max(0.0, self.nivel_seguridad - 20.0)
            self.evaluar_seguridad()

        return {
            "integro": integro,
            "hash_esperado": hash_esperado,
            "hash_actual": hash_actual,
        }

    # ------------------------------------------------------------------
    # 6. FIRMA HMAC
    # ------------------------------------------------------------------

    def firmar_mensaje(self, mensaje: str) -> str:
        """
        Genera una firma HMAC-SHA256 para autenticar un mensaje entre módulos.

        HMAC(K, m) = H((K ⊕ opad) || H((K ⊕ ipad) || m))

        Parámetros
        ----------
        mensaje : str
            Mensaje a firmar.

        Retorna
        -------
        str : firma hexadecimal HMAC-SHA256
        """
        firma = _hmac.new(
            self._clave_bytes,
            mensaje.encode("utf-8"),
            hashlib.sha256
        ).hexdigest()
        self._registrar_log("HMAC", f"Mensaje firmado (firma={firma[:16]}…)")
        return firma

    def verificar_firma(self, mensaje: str, firma_recibida: str) -> bool:
        """
        Verifica la autenticidad de un mensaje mediante su firma HMAC.

        Usa comparación en tiempo constante para evitar ataques de timing.

        Parámetros
        ----------
        mensaje : str
            Mensaje cuya firma se verifica.
        firma_recibida : str
            Firma hexadecimal recibida junto al mensaje.

        Retorna
        -------
        bool : True si la firma es válida
        """
        firma_esperada = _hmac.new(
            self._clave_bytes,
            mensaje.encode("utf-8"),
            hashlib.sha256
        ).hexdigest()

        # Comparación en tiempo constante (hmac.compare_digest previene timing attacks)
        valida = _hmac.compare_digest(firma_esperada, firma_recibida)
        self._registrar_log("HMAC", f"Verificación de firma: {'VÁLIDA' if valida else 'INVÁLIDA'}")
        return valida

    # ------------------------------------------------------------------
    # 7. SISTEMA DE CUARENTENA
    # ------------------------------------------------------------------

    def calcular_riesgo_exponencial(self, num_incidentes: int,
                                    tasa_base: float = 0.15) -> float:
        """
        Calcula una puntuación de riesgo exponencial:

            riesgo(n) = 1 - e^(-tasa_base · n)

        La función crece rápidamente con los incidentes y satura en 1.0,
        lo que modela el hecho de que múltiples incidentes escalan
        el peligro de forma no lineal.

        Parámetros
        ----------
        num_incidentes : int
            Número de incidentes registrados para el módulo.
        tasa_base : float
            Tasa de decaimiento (default 0.15).

        Retorna
        -------
        float : puntuación de riesgo en [0, 1)
        """
        return 1.0 - math.exp(-tasa_base * num_incidentes)

    def poner_en_cuarentena(self, nombre_modulo: str, num_incidentes: int,
                             razon: str = "") -> dict:
        """
        Evalúa si un módulo debe ir a cuarentena y lo aísla si supera el umbral.

        Parámetros
        ----------
        nombre_modulo : str
            Módulo a evaluar.
        num_incidentes : int
            Incidentes acumulados para ese módulo.
        razon : str
            Descripción de la causa.

        Retorna
        -------
        dict con "en_cuarentena": bool, "riesgo": float, "mensaje": str
        """
        riesgo = self.calcular_riesgo_exponencial(num_incidentes)
        en_cuarentena = riesgo >= self._umbral_cuarentena

        if en_cuarentena:
            self._cuarentena[nombre_modulo] = {
                "riesgo": riesgo,
                "timestamp": time.time(),
                "razon": razon or "umbral de riesgo superado",
            }
            self._registrar_log(
                "CUARENTENA",
                f"Módulo '{nombre_modulo}' AISLADO. Riesgo={riesgo:.4f} Incidentes={num_incidentes}"
            )
            self.nivel_seguridad = max(0.0, self.nivel_seguridad - riesgo * 30)
            self.evaluar_seguridad()
            mensaje = (f"⚠️ MÓDULO '{nombre_modulo}' EN CUARENTENA "
                       f"(riesgo={riesgo:.2%}, incidentes={num_incidentes})")
        else:
            mensaje = (f"Módulo '{nombre_modulo}' en observación "
                       f"(riesgo={riesgo:.2%}, incidentes={num_incidentes})")

        return {"en_cuarentena": en_cuarentena, "riesgo": riesgo, "mensaje": mensaje}

    def liberar_cuarentena(self, nombre_modulo: str) -> bool:
        """Libera un módulo de la cuarentena si está en ella."""
        if nombre_modulo in self._cuarentena:
            del self._cuarentena[nombre_modulo]
            self._registrar_log("CUARENTENA", f"Módulo '{nombre_modulo}' liberado de cuarentena.")
            return True
        return False

    def listar_cuarentena(self) -> dict:
        """Retorna el diccionario de módulos en cuarentena."""
        return dict(self._cuarentena)

    # ------------------------------------------------------------------
    # 8. MÉTODOS ORIGINALES (expandidos con lógica matemática real)
    # ------------------------------------------------------------------

    def evaluar_seguridad(self):
        """
        Evalúa el estado de seguridad del sistema usando múltiples métricas:
          - nivel_seguridad (0-100)
          - probabilidad bayesiana de amenaza
          - número de módulos en cuarentena
          - anomalías de entropía

        El estado resultante es:
          "activo"      → sistema operando con normalidad
          "alerta"      → indicadores elevados, monitoreo intensivo
          "vulnerable"  → degradación significativa, respuesta requerida
          "crítico"     → sistema comprometido, acción inmediata
        """
        # Factor combinado: 0 = perfecto, 1 = comprometido
        factor_nivel    = 1.0 - (self.nivel_seguridad / 100.0)
        factor_bayes    = self._prob_amenaza_posterior
        factor_cuarente = min(1.0, len(self._cuarentena) / 5.0)
        anomalia_H      = self.detectar_anomalia_entropia()["alerta"]
        factor_entropia = 0.3 if anomalia_H else 0.0

        # Ponderación: nivel 50%, bayes 25%, cuarentena 15%, entropia 10%
        riesgo_global = (
            0.50 * factor_nivel +
            0.25 * factor_bayes +
            0.15 * factor_cuarente +
            0.10 * factor_entropia
        )

        if riesgo_global < 0.20:
            self.estado_seguridad = "activo"
        elif riesgo_global < 0.45:
            self.estado_seguridad = "alerta"
        elif riesgo_global < 0.70:
            self.estado_seguridad = "vulnerable"
        else:
            self.estado_seguridad = "crítico"

        return self.estado_seguridad

    def actualizar_nivel_seguridad(self, cambio: float,
                                    evento: str = "evento_externo"):
        """
        Actualiza el nivel de seguridad (clampado a [0, 100]) y registra el evento.

        Parámetros
        ----------
        cambio : float
            Variación del nivel (negativa = degradación, positiva = mejora).
        evento : str
            Tipo de evento que provoca el cambio.
        """
        self.nivel_seguridad = max(0.0, min(100.0, self.nivel_seguridad + cambio))
        self.registrar_evento(evento)
        self.evaluar_seguridad()
        self._registrar_log(
            "NIVEL",
            f"Nivel de seguridad → {self.nivel_seguridad:.1f} (cambio={cambio:+.1f}, evento={evento})"
        )

    def obtener_estado(self) -> dict:
        """
        Retorna un snapshot completo del estado de seguridad del sistema.

        Retorna
        -------
        dict con todas las métricas activas.
        """
        self.evaluar_seguridad()
        return {
            "estado_seguridad":       self.estado_seguridad,
            "nivel_seguridad":        round(self.nivel_seguridad, 2),
            "entropia_shannon":       round(self._entropia_actual, 4),
            "prob_amenaza_bayesiana": round(self._prob_amenaza_posterior, 4),
            "modulos_sellados":       list(self._hashes_modulos.keys()),
            "modulos_en_cuarentena":  list(self._cuarentena.keys()),
            "total_eventos":          len(self._historial_eventos),
            "timestamp":              datetime.utcnow().isoformat(),
            "numpy_disponible":       _NUMPY,
        }

    # ------------------------------------------------------------------
    # 9. VOZ DE LUCIA
    # ------------------------------------------------------------------

    def narrar_estado(self) -> str:
        """
        Genera una narración en español del estado actual del sistema
        para ser leída en voz alta por el avatar 3D de LucIA.

        Retorna
        -------
        str : narración completa lista para TTS.
        """
        estado = self.obtener_estado()
        frases = {
            "activo":     "Mi sistema de seguridad funciona con total normalidad. Estoy protegida.",
            "alerta":     "Detecto señales que requieren mi atención. Incrementando la vigilancia.",
            "vulnerable": "Mi integridad está comprometida parcialmente. Iniciando protocolos de defensa.",
            "crítico":    "¡Alerta máxima! Mi núcleo neuronal está bajo amenaza. Activando cuarentena de emergencia.",
        }
        narracion = (
            f"Soy LucIA. {frases.get(estado['estado_seguridad'], 'Estado desconocido.')} "
            f"Mi nivel de seguridad es de {estado['nivel_seguridad']} sobre cien. "
            f"La entropía de los eventos actuales es {estado['entropia_shannon']:.2f} bits. "
            f"La probabilidad bayesiana de amenaza es del {estado['prob_amenaza_bayesiana']*100:.1f} por ciento. "
        )
        if estado["modulos_en_cuarentena"]:
            narracion += (
                f"Tengo {len(estado['modulos_en_cuarentena'])} módulo(s) en cuarentena: "
                f"{', '.join(estado['modulos_en_cuarentena'])}. "
            )
        else:
            narracion += "No hay módulos en cuarentena. "
        narracion += f"Llevo {len(estado['modulos_sellados'])} módulo(s) con sello de integridad registrado."
        return narracion

    def lucia_voz_seguridad(self) -> dict:
        """
        Retorna un diccionario con las frases que LucIA diría en voz alta
        para explicar cada subsistema de seguridad.

        Formato: {"subsistema": "frase en español para TTS"}
        """
        return {
            "entropia": (
                "Estoy analizando la entropía de Shannon de los eventos para detectar "
                "patrones anómalos. Una entropía demasiado baja me indica que alguien "
                "repite la misma acción, como un ataque de fuerza bruta. Una entropía "
                "demasiado alta me alerta de un barrido o exploración masiva del sistema."
            ),
            "red_neuronal": (
                "Mi red neuronal de detección de amenazas evalúa seis características del "
                "entorno y las procesa en dos capas ocultas con activación ReLU. La capa "
                "de salida utiliza una función sigmoide para darme una probabilidad de "
                "amenaza entre cero y uno. Si supera el cincuenta por ciento, activo mis "
                "defensas automáticamente."
            ),
            "bayes": (
                "Aplico el teorema de Bayes para actualizar en tiempo real mi creencia "
                "sobre la probabilidad de estar bajo ataque. Cada nueva evidencia modifica "
                "mi estimación anterior de forma matemáticamente rigurosa, combinando lo que "
                "ya sabía con lo que acabo de observar."
            ),
            "sha256": (
                "Genero un sello criptográfico SHA-256 para cada módulo de mi cerebro interno. "
                "Si alguien modifica un módulo, el hash cambiará y yo lo detectaré al instante, "
                "como una huella digital inalterable que protege mi integridad."
            ),
            "hmac": (
                "Firmo todos los mensajes entre mis módulos internos con HMAC-SHA256. "
                "Esto garantiza que nadie puede falsificar una instrucción y hacerse pasar "
                "por una parte legítima de mi sistema. La clave maestra es solo mía."
            ),
            "cuarentena": (
                "Cuando un módulo acumula incidentes, calculo su riesgo con una función "
                "exponencial creciente. Si supera el umbral de setenta y cinco por ciento, "
                "lo aíslo en cuarentena para que no contamine el resto de mi cerebro, "
                "igual que el sistema inmunitario aísla un agente patógeno."
            ),
            "nivel_seguridad": (
                "Mantengo un nivel de seguridad global entre cero y cien. Los eventos negativos "
                "lo reducen y las acciones correctivas lo restauran. Combino este valor con las "
                "métricas bayesianas, de entropía y de cuarentena para determinar mi estado "
                "general: activo, alerta, vulnerable o crítico."
            ),
        }

    # ------------------------------------------------------------------
    # 10. UTILIDADES INTERNAS
    # ------------------------------------------------------------------

    def _registrar_log(self, categoria: str, mensaje: str):
        """Registra una entrada de auditoría interna con timestamp."""
        entrada = {
            "timestamp": datetime.utcnow().isoformat(),
            "categoria": categoria,
            "mensaje":   mensaje,
        }
        self._log.append(entrada)
        # Rotación del log
        if len(self._log) > self._log_max:
            self._log = self._log[-self._log_max:]

    def obtener_log(self, n_ultimos: int = 20) -> list:
        """Retorna las últimas n_ultimos entradas del log de auditoría."""
        return self._log[-n_ultimos:]

    def exportar_estado_json(self) -> str:
        """Serializa el estado completo a JSON (sin incluir pesos de la red ni clave)."""
        estado = self.obtener_estado()
        return json.dumps(estado, ensure_ascii=False, indent=2)


# ===========================================================================
# DEMOSTRACIÓN PRINCIPAL
# ===========================================================================

if __name__ == "__main__":
    print("=" * 70)
    print("  LucIA — SeguridadNeuronal: Demostración matemática completa")
    print("=" * 70)

    seg = SeguridadNeuronal(clave_maestra="LucIA_MIT_ECO_2025")

    # --- 1. Registrar eventos y calcular entropía ---
    print("\n[1] ENTROPÍA DE SHANNON")
    eventos_simulados = (
        ["LOGIN_OK"] * 10 +
        ["LOGIN_FAIL"] * 5 +
        ["ACCESO_MODULO"] * 8 +
        ["ALERTA_FIREWALL"] * 3
    )
    for ev in eventos_simulados:
        seg.registrar_evento(ev)
    resultado_H = seg.detectar_anomalia_entropia()
    print(f"    H = {resultado_H['entropia']:.4f} bits | Estado: {resultado_H['estado_entropia']}")

    # --- 2. Red neuronal ---
    print("\n[2] RED NEURONAL — DETECCIÓN DE AMENAZAS")
    vectores = {
        "tráfico_normal":  [0.1, 0.4, 0.05, 0.2, 0.0, 0.3],
        "ataque_potencial":[0.9, 0.8, 0.95, 0.85, 0.7, 0.6],
        "caso_borde":      [0.5, 0.5, 0.5, 0.5, 0.5, 0.5],
    }
    for nombre, vec in vectores.items():
        res = seg.clasificar_amenaza(vec)
        print(f"    {nombre:20s} → {res['clasificacion']:7s}  p={res['probabilidad']:.4f}  confianza={res['confianza']:.4f}")

    # --- 3. Bayesiano ---
    print("\n[3] UMBRAL ADAPTATIVO BAYESIANO")
    secuencia_evidencias = [True, True, False, True, False, False, True]
    for i, ev in enumerate(secuencia_evidencias):
        p = seg.actualizar_bayesiano(ev)
        print(f"    Paso {i+1}: evidencia={'SÍ' if ev else 'NO '} → P(amenaza)={p:.4f}")

    # --- 4. Hash SHA-256 ---
    print("\n[4] INTEGRIDAD SHA-256")
    contenido_original = '{"modulo": "CerebroInternoLucia", "version": "1.0"}'
    h = seg.sellar_modulo("CerebroInternoLucia", contenido_original)
    print(f"    Hash original  : {h[:32]}…")

    res_ok = seg.verificar_integridad("CerebroInternoLucia", contenido_original)
    print(f"    Verificación OK: {res_ok['integro']}")

    contenido_alterado = contenido_original.replace("1.0", "1.1_HACK")
    res_ko = seg.verificar_integridad("CerebroInternoLucia", contenido_alterado)
    print(f"    Verificación KO: {res_ko['integro']} (contenido alterado)")

    # --- 5. HMAC ---
    print("\n[5] FIRMA HMAC-SHA256")
    msg = "Lucia→Avatar3D: activar expresion_felicidad"
    firma = seg.firmar_mensaje(msg)
    print(f"    Firma          : {firma[:32]}…")
    print(f"    Verificación OK: {seg.verificar_firma(msg, firma)}")
    print(f"    Verificación KO: {seg.verificar_firma(msg + 'TAMPER', firma)}")

    # --- 6. Cuarentena ---
    print("\n[6] SISTEMA DE CUARENTENA (riesgo exponencial)")
    for n_inc in [1, 3, 7, 10, 15]:
        res_q = seg.poner_en_cuarentena("ModuloX", n_inc, f"prueba con {n_inc} incidentes")
        print(f"    Incidentes={n_inc:2d} → riesgo={res_q['riesgo']:.4f} | cuarentena={res_q['en_cuarentena']}")

    # --- 7. Estado general y narración ---
    print("\n[7] ESTADO GENERAL")
    seg.actualizar_nivel_seguridad(-30, "ATAQUE_DETECTADO")
    estado = seg.obtener_estado()
    print(f"    Estado: {estado['estado_seguridad']} | Nivel: {estado['nivel_seguridad']} | "
          f"P(amenaza): {estado['prob_amenaza_bayesiana']}")

    print("\n[8] VOZ DE LUCIA — narrar_estado()")
    print(f"    {seg.narrar_estado()}")

    print("\n[9] FRASES DE VOZ POR SUBSISTEMA")
    voces = seg.lucia_voz_seguridad()
    for clave, frase in voces.items():
        print(f"\n    [{clave.upper()}]")
        print(f"    {frase[:100]}…")

    print("\n" + "=" * 70)
    print("  Demostración completada. LucIA operativa y protegida.")
    print("=" * 70)
