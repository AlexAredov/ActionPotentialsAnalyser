"""
Библиотека для анализа потенциалов действий кардиомиоцитов

Параметры, которые меняет пользователь (если ничего не введено, используются значения по-умолчанию):
    1) preprocess:              smooth
    2) find_action_potentials:  alpha_threshold, refractory period, start_offset
    3) plot_phase_4_speeds:     min_time_diff
    4) plot_phase_0_speeds:     min_time_diff
    5) plot_radiuses:           min_time_diff

Пояснение:
    1)  smooth - сглаживание изначальных данных
    2)  alpha - значимая скорость изменения напряжения
    3)  refractory_perid - период, в течение которого алгоритм не будет считать 
        величину данного напряжение за следующий потенциал действия
    4)  start_offset - смещение по времени начала потенциала действия
    5)  min_time_diff - время (указывать в минутах) между точками, чтобы график не был таки шумным

Функции:
----------
preprocess(file: str, smooth: int = 15) -> Tuple[np.ndarray, np.ndarray]:
    Предобработка входных данных, сглаживание на основе полной вариации и фильтра гаусса

find_action_potentials(time: np.ndarray, voltage: np.ndarray, alpha_threshold: int = 25, refractory_period: int = 100, start_offset: int = 80) -> List[Dict[str, int]]:
    Определение потенциалов действий

find_voltage_speed(ap: Dict[str, int], time: np.ndarray, voltage: np.ndarray) -> Tuple[float, float]:
    Расчет средних скоростей в фазах 4 и 0

circle(time: np.ndarray, voltage: np.ndarray) -> Tuple[float, float, float]:
    Расчет радиуса кривизны потенциала действия и координат центр окружности

plot_ap(ap: Dict[str, int], time: np.ndarray, voltage: np.ndarray):
    Вывод графика потенциала действия

plot_phase_4_speed(ap: Dict[str, int], time: np.ndarray, voltage: np.ndarray):
    Вывод графика скорости изменения напряжения фазы 4 для определенного потенциала действия

plot_phase_0_speed(ap: Dict[str, int], time: np.ndarray, voltage: np.ndarray):
    Вывод графика скорости изменения напряжения фазы 0 для определенного потенциала действия

plot_phase_4_speeds(aps: List[Dict[str, int]], time: np.ndarray, voltage: np.ndarray, min_time_diff: float = 0):
    Вывод графика скорости изменения напряжения фазы 4 для всех потенциалов действий
    Параметр "min_time_diff" - задает временной промежуток между значениями на графике

plot_phase_0_speeds(aps: List[Dict[str, int]], time: np.ndarray, voltage: np.ndarray, min_time_diff: float = 0):
    Вывод графика скорости изменения напряжения фазы 0 для всех потенциалов действий
    Параметр "min_time_diff" - задает временной промежуток между значениями на графике

def plot_radiuses(aps: List[Dict[str, int]], time: np.ndarray, voltage: np.ndarray, min_time_diff: float = 0):
    Вывод графика радиусов для всех потенциалов действий

save_aps_to_xlsx(destination: str, aps: List[Dict[str, int]], time: np.ndarray, voltage: np.ndarray):
    Сохранение данных потенциалов действий в Excel таблицу
"""

import numpy as np
import matplotlib.pyplot as plt
import pandas as pd
import openpyxl
import os
from scipy.ndimage import gaussian_filter
from io import StringIO
from skimage.restoration import denoise_tv_chambolle
from math import sqrt
from itertools import islice
from openpyxl.utils import get_column_letter

def open_txt(file):
    with open(file, 'r') as file:
        file_content = file.read()
    file_content = file_content.replace(',', '.')

    with StringIO(file_content) as file_buffer:
        data = np.genfromtxt(file_buffer, delimiter='\t')

    return data[:,0], data[:,1]

def save_aps_to_txt(destination, aps, time, voltage):
    if not os.path.exists(destination):
        os.makedirs(destination)

    time_intervals = []

    for number, ap in enumerate(aps):
        current_time = time[ap['pre_start']:ap['end']]
        current_voltage = voltage[ap['pre_start']:ap['end']]

        time_intervals.append([current_time[0], current_time[-1]])

        with open(os.path.join(destination, f"{number+1}.txt"), 'w') as f:
            for i in range(current_time.shape[0]):
                f.write(f"{current_time[i]}\t{current_voltage[i]}\n")

    return time_intervals

def save_aps_to_xlsx(destination, aps, time, voltage):
    data = []

    for number, ap in enumerate(aps):
        phase_4_speed, phase_0_speed = find_voltage_speed(ap, time, voltage)
        
        current_ap_time = time[ap['pre_start']:ap['end']]
        current_ap_voltage = voltage[ap['pre_start']:ap['end']]
        
        radius, _, _ = circle(current_ap_time, current_ap_voltage)
        
        row = {
            "Номер ПД": number + 1,
            "Начало": f"{current_ap_time[0]:.2f}",
            "Конец": f"{current_ap_time[-1]:.2f}",
            "Радиус": round(radius, 2),
            "dV/dt4": phase_4_speed,
            "dV/dt0": phase_0_speed
        }
        data.append(row)

    df = pd.DataFrame(data)
    df.to_excel(destination, index=False, engine='openpyxl')

def preprocess(file, smooth = 15):
    time, voltage = open_txt(file)

    time = time * 1000
    voltage = denoise_tv_chambolle((voltage * 1000), smooth)
    voltage = gaussian_filter(voltage, 1)

    return time, voltage

def find_action_potentials(time, voltage, alpha_threshold = 25, refractory_period = 10, start_offset = 80):
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

def circle(time, voltage):
    #plt.style.use('seaborn-whitegrid')

    def nearest_value(items_x, items_y, value_x, value_y):
        l = []
        for i in range(len(items_x)):
            l.append(sqrt((value_x - items_x[i])**2 + (value_y - items_y[i])**2))
        return(l.index(min(l)))

    def flat(x, y, n):
        x1 = []
        y1 = []
        for i in range(0,len(x),n):
            x1.append(x[i])
            y1.append(y[i])
        dat = [x1, y1]
        return dat

    def radius(x_c, y_c, x_1, y_1, x_2, y_2):
        cent1_x = (x_c + x_1)/2
        cent1_y = (y_c + y_1)/2
        cent2_x = (x_c + x_2)/2
        cent2_y = (y_c + y_2)/2

        k1 = (cent1_x - x_c)/(cent1_y - y_c)
        b1 = cent1_y + k1*cent1_x

        k2 = (cent2_x - x_c)/(cent2_y - y_c)
        b2 = cent2_y + k2*cent2_x

        x_r = (b2-b1)/(k2-k1)
        y_r = -k1*x_r + b1

        rad = sqrt((x_r - x_c)**2 + (y_r - y_c)**2)

        #plt.gca().add_patch(plt.Circle((x_r, y_r), rad, color='lightblue', alpha=0.5))
        return rad, x_r, y_r

    x_t = list(time)
    y_t = list(voltage)

    x = []
    y = []
    for i in range(len(x_t)):
        if (x_t[i] < x_t[list(y_t).index(max(y_t))]) and (y_t[i] < min(y_t)/2):
            x.append(x_t[i])
            y.append(y_t[i])

    l = 32
    dff = flat(x, y, 32)
    ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))

    o = 10

    while ma + o >= len(dff[0]):
        l = l//2
        dff = flat(x, y, l)
        ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))
    while dff[1][ma+o] - dff[1][ma] >= 3:
        o -= 1
        ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))

    #set the size of graph
    #f = plt.figure()
    #f.set_figwidth(5*2*max(dff[0])/abs(min(dff[1])))
    #f.set_figheight(5)
    rad, x_r, y_r = radius(dff[0][ma], dff[1][ma], dff[0][ma+o], dff[1][ma+o], dff[0][ma-o], dff[1][ma-o])
    return rad, x_r, y_r

def plot_ap(ap, time, voltage, number = ''):
    current_ap_time = time[ap['pre_start']:ap['end']]
    current_ap_voltage = voltage[ap['pre_start']:ap['end']]

    plt.figure()
    plt.scatter(time[ap['pre_start']], voltage[ap['pre_start']], marker='o', color='black')
    plt.scatter(time[ap['start']], voltage[ap['start']], marker='o', color='red')
    plt.scatter(time[ap['peak']], voltage[ap['peak']], marker='o', color='green')
    plt.scatter(time[ap['end']], voltage[ap['end']], marker='o', color='black')
    plt.plot(current_ap_time, current_ap_voltage)
    plt.xlabel("Время (мс)")
    plt.ylabel("Напряжение (мВ)")
    plt.title(f"{number} ПД")
    plt.show()

def plot_aps(aps, time, voltage):
    plt.figure()
    for ap in aps:
        pre_start_index = ap['pre_start']
        end_index = ap['end']
        ap_time = time[pre_start_index:end_index+1]
        ap_voltage = voltage[pre_start_index:end_index+1]

        plt.plot(ap_time, ap_voltage)

    plt.xlabel("Время (мс)")
    plt.ylabel("Напряжение (мВ)")
    plt.title("Найденные ПД")
    plt.show()

def plot_phase_4_speed(ap, time, voltage):
    plt.figure()

    phase_4_time = time[ap['pre_start']:ap['start']]
    phase_4_voltage = voltage[ap['pre_start']:ap['start']]
    phase_4_speed = np.diff(phase_4_voltage) / np.diff(phase_4_time)

    plt.plot(phase_4_time[:-1], phase_4_speed)
    plt.show()

def plot_phase_0_speed(ap, time, voltage):
    plt.figure()

    phase_0_time = time[ap['start']:ap['peak']]
    phase_0_voltage = voltage[ap['start']:ap['peak']]
    phase_0_speed = np.diff(phase_0_voltage) / np.diff(phase_0_time)

    plt.plot(phase_0_time[:-1], phase_0_speed)
    plt.show()

def plot_phase_4_speeds(aps, time, voltage, min_time_diff = 0):
    plt.figure()

    phase_4_times = []
    phase_4_speeds = []

    min_time_diff = min_time_diff * 60 * 1000

    for ap in aps:
        phase_4_time = time[ap['pre_start']]
        if not phase_4_times or phase_4_time - phase_4_times[-1] >= min_time_diff:
            phase_4_times.append(phase_4_time)
            phase_4_speed, _ = find_voltage_speed(ap, time, voltage)
            phase_4_speeds.append(phase_4_speed)

    plt.plot(phase_4_times, phase_4_speeds)

    plt.xlabel("Время (мс)")
    plt.ylabel("Скорость изменения напряжения (мВ/мс)")
    plt.title("Фаза 4")
    plt.show()

def plot_phase_0_speeds(aps, time, voltage, min_time_diff = 0):
    plt.figure()

    phase_0_times = []
    phase_0_speeds = []

    min_time_diff = min_time_diff * 60 * 1000

    for ap in aps:
        phase_0_time = time[ap['start']]
        if not phase_0_times or phase_0_time - phase_0_times[-1] >= min_time_diff:
            phase_0_times.append(phase_0_time)
            _, phase_0_speed = find_voltage_speed(ap, time, voltage)
            phase_0_speeds.append(phase_0_speed)

    plt.plot(phase_0_times, phase_0_speeds)

    plt.xlabel("Время (мс)")
    plt.ylabel("Скорость изменения напряжения (мВ/мс)")
    plt.title("Фаза 0")
    plt.show()

def plot_radiuses(aps, time, voltage, min_time_diff = 0):
    plt.figure()

    times = []
    radiuses = []

    min_time_diff = min_time_diff * 60 * 1000

    for ap in aps:
        current_ap_time = time[ap['pre_start']]
        current_ap_voltage = voltage[ap['pre_start']:ap['end']]
        if not times or current_ap_time - times[-1] >= min_time_diff:
            times.append(current_ap_time)
            current_ap_radius, _, _ = circle(time[ap['pre_start']:ap['end']], current_ap_voltage)
            radiuses.append(current_ap_radius)

    plt.plot(times, radiuses)

    plt.xlabel("Время (мс)")
    plt.ylabel("Радиус кривизны ПД (у.е.)")
    plt.show()