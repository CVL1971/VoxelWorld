# Causa Raíz: Franja Sin Geometría (16 chunks entre líneas)

**Estado**: **Causa localizada**. Desaparece con chunk 17×17×17; el chunk central estaba desalineado del centro. Refactor aplicada.

---

## Causa identificada

- Franja debida a **desalineación del chunk central** respecto al centro.
- Con chunk **17×17×17** el problema desaparece (centro alineado).
- **RecycleLayerX** iteraba todo el dominio (O(n)) en vez de solo la capa afectada.

---

## RecycleLayerX: por qué O(N) es necesario

El grid usa un array físico fijo (mChunks). Cada chunk tiene:
- **mCoord** (slot): fijo, no cambia.
- **mGlobalCoord**: dinámico, cambia al reciclar.

**No existe mapeo directo** globalX → slotX. localX ≠ globalX - mActiveMin.x porque el grid no es un buffer circular.

**Para optimizar** haría falta:
1. Introducir un offset de buffer circular en X, o
2. Mantener un índice auxiliar globalX → índices físicos.

Sin esa estructura, el scan O(N) es el único método seguro y determinista.

---

## Hipótesis descartadas (no eran la causa)

| Hipótesis | Resultado |
|-----------|-----------|
| TryAdd rechaza en RenderQueueAsync | No aparecían logs |
| Mapeo GetChunkByGlobalCoord | Negativo |
