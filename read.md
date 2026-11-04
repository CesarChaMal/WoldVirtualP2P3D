Hola:

Es la primera vez que participo en este proyecto. Le respondo después de revisar la especificación y la nota de la versión 2.7.6 del 4 de noviembre.

Según esa nota, la 2.7.6 cierra el punto del portal (W-01 / DT-11). WebNodo lee el release.json publicado con el esquema {assets[]}, /descargar sirve el binario en ejecución con el nombre anunciado, sin redirecciones 301, y la release vigente se refresca cada 5 minutos. /api/info muestra la versión publicada y la versión instalada. Se añade la autoprueba --webnodo-selftest, el proyecto xUnit Visor3DTests queda con 34 pruebas superadas, y la pantalla de inicio detecta si ya hay un nodo activo en el mismo equipo.

El ejecutable publicado es DMWoldVirtualP2P3Dv2.7.6.exe, con SHA-256 26fb446e45a937af2a6c8866efb9601ef0ec0b192d105f33aba2e01d973b150f. La release incluye también release.json y SHA256SUMS.

Con eso, puedo ayudar en este orden:

Preparar un guion breve de prueba entre dos equipos, en red local y por túnel, y anotar los resultados: presencia, avatar, chat, amigos, voz, webcam y construcción.
Comprobar en la v2.7.6 /api/info, /descargar y --webnodo-selftest, también a través de un túnel.
Ordenar los pendientes de la hoja de ruta en una lista corta para el siguiente ciclo.
Cuando usted lo indique, redactar por separado los criterios de aceptación de LucIA, de la identidad única y de WCVcoin.
Para empezar, le agradecería que me confirmara:

Si la prueba entre dos equipos es la primera tarea.
Por dónde prefiere que empiece: el guion de pruebas, la revisión del portal o la lista de prioridades.
El plazo orientativo, y si el alcance es Windows, Linux o ambos. En la v2.7.6 los archivos publicados son el ejecutable de Windows, release.json y SHA256SUMS.
Quedo atento a su respuesta.

Un saludo,
