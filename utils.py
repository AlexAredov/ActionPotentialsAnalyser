"""
Библиотека для анализа потенциалов действий кардиомиоцитов

Функции:
----------
preprocess(file: str, smooth: int = 15) -> Tuple[np.ndarray, np.ndarray]:
    Предобработка входных данных, сглаживание на основе полной вариации

find_action_potentials(time: np.ndarray, voltage: np.ndarray, alpha_threshold: int = 25, refractory_period: int = 100, start_offset: int = 80) -> List[Dict[str, int]]:
    Определение потенциалов действий, благодаря параметрам:
    1)  alpha - значимая скорость изменения напряжения
    2)  refractory_perid - период, в течение которого алгоритм не будет считать величину данного напряжение (значимого или незначимого)
        за следующий потенциал действия
    3)  start_offset - смещение по времени начала потенциала действия

find_voltage_speed(ap: Dict[str, int], time: np.ndarray, voltage: np.ndarray) -> Tuple[float, float]:
    Расчет средних скоростей в фазах 4 и 0

plot_ap(ap: Dict[str, int], time: np.ndarray, voltage: np.ndarray):
    Вывод графика потенциала действия

plot_phase_4_speed(aps: List[Dict[str, int]], time: np.ndarray, voltage: np.ndarray):
    Вывод графика скорости изменения напряжения фазы 4 для каждого потенциала действия

plot_phase_0_speed(aps: List[Dict[str, int]], time: np.ndarray, voltage: np.ndarray):
    Вывод графика скорости изменения напряжения фазы 0 для каждого потенциала действия

save_aps_to_xlsx(destination: str, aps: List[Dict[str, int]], time: np.ndarray, voltage: np.ndarray):
    Сохранение данных потенциалов действий в Excel таблицу
"""

import numpy as np
import matplotlib.pyplot as plt
import pandas as pd
import openpyxl
from scipy.ndimage import gaussian_filter
from io import StringIO
from skimage.restoration import denoise_tv_chambolle
from math import sqrt
from openpyxl.utils import get_column_letter

def preprocess(file, smooth = 15):
    with open("source/" + file, 'r') as file:
        file_content = file.read()
    file_content = file_content.replace(',', '.')

    with StringIO(file_content) as file_buffer:
        data = np.genfromtxt(file_buffer, delimiter='\t')

    time = data[:, 0] * 1000
    voltage = denoise_tv_chambolle((data[:, 1] * 1000), smooth)
    voltage = gaussian_filter(voltage, 1)
    return time, voltage

def find_action_potentials(time, voltage, alpha_threshold = 25, refractory_period = 100, start_offset = 80):
    # "pre_start" - нулевое или время конца предыдущего ПД
    # "start" - непосредственно начало фазы 0
    # "peak" - максимум ПД
    # "end" - минимум фазы 4
    voltage_derivative = np.diff(voltage) / np.diff(time)

    candidate_phase_0_start_indices = np.where(voltage_derivative > alpha_threshold)[0]
    phase_0_start_indices = []
    for i in range(len(candidate_phase_0_start_indices)):
        if i == 0 or candidate_phase_0_start_indices[i] - candidate_phase_0_start_indices[i-1] > refractory_period:
            phase_0_start_indices.append(candidate_phase_0_start_indices[i])

    action_potentials = []
    for i, start_index in enumerate(phase_0_start_indices):
        pre_start_index = action_potentials[-1]['end'] if i > 0 else 0

        if i < len(phase_0_start_indices) - 1:
            next_start_index = phase_0_start_indices[i + 1]
            peak_index = np.argmax(voltage[start_index:next_start_index]) + start_index
            end_index = np.argmin(voltage[peak_index:next_start_index]) + peak_index
        else:
            peak_index = np.argmax(voltage[start_index:]) + start_index
            end_index = np.argmin(voltage[peak_index:]) + peak_index

        action_potentials.append({
            'pre_start': pre_start_index,
            'start': start_index - start_offset,
            'peak': peak_index,
            'end': end_index
        })

    return action_potentials

def find_voltage_speed(ap, time, voltage):
    prestart_index = ap['pre_start']
    start_index = ap['start']
    peak_index = ap['peak']
    
    phase_4_time = time[prestart_index:start_index]
    phase_4_voltage = voltage[prestart_index:start_index]
    phase_4_speed = np.diff(phase_4_voltage) / np.diff(phase_4_time)

    phase_0_time = time[start_index:peak_index]
    phase_0_voltage = voltage[start_index:peak_index]
    phase_0_speed = np.diff(phase_0_voltage) / np.diff(phase_0_time)

    return np.mean(phase_4_speed), np.mean(phase_0_speed)

def plot_ap(ap, time, voltage):
    current_ap_time = time[ap['pre_start']:ap['end']]
    current_ap_voltage = voltage[ap['pre_start']:ap['end']]

    plt.figure()
    plt.scatter(time[ap['start']], voltage[ap['start']], marker='o', color='red')
    plt.plot(current_ap_time, current_ap_voltage)
    plt.xlabel("Время (мс)")
    plt.ylabel("Напряжение (мВ)")
    plt.show("ПД")

def plot_phase_4_speed(aps, time, voltage):
    plt.figure()

    phase_4_times = []
    phase_4_speeds = []

    for ap in aps:
        phase_4_times.append(time[ap['pre_start']])
        phase_4_speed, _ = find_voltage_speed(ap, time, voltage)
        phase_4_speeds.append(phase_4_speed)

    plt.plot(phase_4_times, phase_4_speeds)

    plt.xlabel("Время (мс)")
    plt.ylabel("Скорость изменения напряжения (мВ/мс)")
    plt.title("Фаза 4")
    plt.show()

def plot_phase_0_speed(aps, time, voltage):
    plt.figure()

    phase_0_times = []
    phase_0_speeds = []

    for ap in aps:
        phase_0_times.append(time[ap['start']])
        _, phase_0_speed = find_voltage_speed(ap, time, voltage)
        phase_0_speeds.append(phase_0_speed)

    plt.plot(phase_0_times, phase_0_speeds)

    plt.xlabel("Время (мс)")
    plt.ylabel("Скорость изменения напряжения (мВ/мс)")
    plt.title("Фаза 0")
    plt.show()

def save_aps_to_xlsx(destination, aps, time, voltage):
    data = []

    for number, ap in enumerate(aps):
        phase_4_speed, phase_0_speed = find_voltage_speed(ap, time, voltage)
        
        current_ap_time = time[ap['pre_start']:ap['end']]
        current_ap_voltage = voltage[ap['pre_start']:ap['end']]
        
        # radius, _, _ = circle(current_ap_time, current_ap_voltage)
        
        row = {
            "Номер ПД": number + 1,
            "Начало": f"{current_ap_time[0]:.2f}",
            "Конец": f"{current_ap_time[-1]:.2f}",
            "Радиус": "radius",
            "dV/dt4": phase_4_speed,
            "dV/dt0": phase_0_speed
        }
        data.append(row)

    df = pd.DataFrame(data)
    df.to_excel(destination, index=False, engine='openpyxl')