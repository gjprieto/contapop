import { useQuery } from '@tanstack/react-query';
import { getPlan, getPlans, getPlanVsActual } from './plans-api';
export const planKeys = { all: ['plans'] as const, detail: (id: string) => [...planKeys.all, id] as const, actual: (id: string) => [...planKeys.detail(id), 'actual'] as const };
export const usePlans = () => useQuery({ queryKey: planKeys.all, queryFn: ({ signal }) => getPlans(signal) });
export const usePlan = (id?: string) => useQuery({ queryKey: planKeys.detail(id ?? ''), queryFn: ({ signal }) => getPlan(id!, signal), enabled: !!id });
export const usePlanVsActual = (id?: string) => useQuery({ queryKey: planKeys.actual(id ?? ''), queryFn: ({ signal }) => getPlanVsActual(id!, signal), enabled: !!id });
