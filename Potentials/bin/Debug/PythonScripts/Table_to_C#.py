"""
Библиотека для анализа потенциалов действий кардиомиоцитов
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
import sys
import shutil


def open_txt(file):
    """
    Открывает текстовый файл и возвращает массивы времени и напряжения, разделенных табуляцией.

    Аргументы:
        file (str): Путь к текстовому файлу.

    Возвращает:
        tuple: Кортеж из двух массивов numpy (первый столбец данных, второй столбец данных).
    """
    with open(file, 'r') as file:
        file_content = file.read()
    file_content = file_content.replace(',', '.')

    with StringIO(file_content) as file_buffer:
        data = np.genfromtxt(file_buffer, delimiter='\t', usecols=(0, 1))

    return data[:, 0], data[:, 1]


def save_aps_to_xlsx(destination, aps, time, voltage):
    """
    Сохраняет информацию о ПД в .xlsx таблицу.

    Аргументы:
        destination (str): Путь к папке для сохранения.
        aps (list): Список словарей, содержащих ключи 'pre_start' и 'end'.
        time (np.ndarray): 1D массив numpy содержащий значения времени.
        voltage (np.ndarray): 1D массив numpy содержащий значения напряжения.

    Возвращает:
        None
    """
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


def preprocess(file, smooth=15):
    """
    Предварительная обработка данных из файла.

    Аргументы:
        file (str): Путь к текстовому файлу с исходными данными.
        smooth (int, optional): Параметр сглаживания для алгоритма denoise_tv_chambolle.
            По умолчанию равен 15.

    Возвращает:
        tuple: Кортеж из двух массивов numpy (время и напряжение).
    """
    time, voltage = open_txt(file)

    time = time * 1000
    voltage = denoise_tv_chambolle((voltage * 1000), smooth)
    voltage = gaussian_filter(voltage, 1)

    quality = 3

    return np.round(time, quality), np.round(voltage, quality)


def find_action_potentials(time, voltage, alpha_threshold=25, refractory_period=10, start_offset=80):
    """
    Находит ПД в данных времени и напряжения.

    Функция анализирует производную напряжения по времени, выявляет начало фазы 0,
    пик и конец фазы 4 для каждого потенциала действия. Возвращает список словарей с
    индексами ключевых точек для каждого ПД.

    Аргументы:
        time (np.ndarray): 1D массив numpy содержащий значения времени.
        voltage (np.ndarray): 1D массив numpy содержащий значения напряжения.
        alpha_threshold (float, optional): Пороговое значение производной напряжения
            для определения начала фазы 0. По умолчанию равен 25.
        refractory_period (int, optional): Рефрактерный период между значениями напряжения.
            По умолчанию равен 10.
        start_offset (int, optional): Смещение для определения начала фазы 0.
            По умолчанию равно 80.

    Возвращает:
        list: Список словарей с индексами ключевых точек для каждого ПД.
    """
    voltage_derivative = np.diff(voltage) / np.diff(time)

    candidate_phase_0_start_indices = np.where(voltage_derivative > alpha_threshold)[0]
    phase_0_start_indices = []
    for i in range(len(candidate_phase_0_start_indices)):
        if i == 0 or candidate_phase_0_start_indices[i] - candidate_phase_0_start_indices[i - 1] > refractory_period:
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
    """
    Находит среднюю скорость изменения напряжения для фаз 4 и 0 ПД.

    Аргументы:
        ap (dict): Словарь с индексами ключевых точек ПД.
        time (np.ndarray): 1D массив numpy содержащий значения времени.
        voltage (np.ndarray): 1D массив numpy содержащий значения напряжения.

    Возвращает:
        tuple: Кортеж из двух чисел - средней скорости изменения напряжения для фазы 4 и фазы 0.
    """
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
    """
    Вычисляет радиус и координаты центра окружности, наилучшим образом подходящей для точек на графике.

    Аргументы:
        time (np.ndarray): 1D массив numpy содержащий значения времени.
        voltage (np.ndarray): 1D массив numpy содержащий значения напряжения.

    Возвращает:
        tuple: Кортеж из трех чисел - радиус окружности, координата X и координата Y центра окружности.
    """

    def nearest_value(items_x, items_y, value_x, value_y):
        l = []
        for i in range(len(items_x)):
            l.append(sqrt((value_x - items_x[i]) ** 2 + (value_y - items_y[i]) ** 2))
        return (l.index(min(l)))

    def flat(x, y, n):
        x1 = []
        y1 = []
        for i in range(0, len(x), n):
            x1.append(x[i])
            y1.append(y[i])
        dat = [x1, y1]
        return dat

    def radius(x_c, y_c, x_1, y_1, x_2, y_2):
        cent1_x = (x_c + x_1) / 2
        cent1_y = (y_c + y_1) / 2
        cent2_x = (x_c + x_2) / 2
        cent2_y = (y_c + y_2) / 2

        k1 = (cent1_x - x_c) / (cent1_y - y_c)
        b1 = cent1_y + k1 * cent1_x

        k2 = (cent2_x - x_c) / (cent2_y - y_c)
        b2 = cent2_y + k2 * cent2_x

        x_r = (b2 - b1) / (k2 - k1)
        y_r = -k1 * x_r + b1

        rad = sqrt((x_r - x_c) ** 2 + (y_r - y_c) ** 2)

        return rad, x_r, y_r

    x_t = list(time)
    y_t = list(voltage)

    x = []
    y = []
    for i in range(len(x_t)):
        if (x_t[i] < x_t[list(y_t).index(max(y_t))]) and (y_t[i] < min(y_t) / 2):
            x.append(x_t[i])
            y.append(y_t[i])

    l = 32
    dff = flat(x, y, 32)
    ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))

    o = 10

    while ma + o >= len(dff[0]):
        l = l // 2
        dff = flat(x, y, l)
        ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))
    while dff[1][ma + o] - dff[1][ma] >= 3:
        o -= 1
        ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))

    rad, x_r, y_r = radius(dff[0][ma], dff[1][ma], dff[0][ma + o], dff[1][ma + o], dff[0][ma - o], dff[1][ma - o])
    return rad, x_r, y_r


def plot_ap(ap, time, voltage, number=''):
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
        ap_time = time[pre_start_index:end_index + 1]
        ap_voltage = voltage[pre_start_index:end_index + 1]

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


def plot_phase_4_speeds(aps, time, voltage, min_time_diff=0):
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


def plot_phase_0_speeds(aps, time, voltage, min_time_diff=0):
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


def plot_radiuses(aps, time, voltage, min_time_diff=0):
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


def save_aps_to_txt(destination, aps, time, voltage):
    """
    Сохраняет ПД в текстовый файл в определенную папку.

    Аргументы:
        destination (str): Путь к папке для сохранения.
        aps (list): Список словарей, содержащих ключи 'pre_start' и 'end'.
        time (np.ndarray): 1D массив numpy содержащий значения времени.
        voltage (np.ndarray): 1D массив numpy содержащий значения напряжения.

    Возвращает:
        list: Лист временных интервалов для начала и конца каждого ПД
    """
    if os.path.exists(destination):
        shutil.rmtree(destination)

    if not os.path.exists(destination):
        os.makedirs(destination)

    time_intervals = []

    for number, ap in enumerate(aps):
        current_time = time[ap['pre_start']:ap['end']]
        current_voltage = voltage[ap['pre_start']:ap['end']]

        time_intervals.append([round(current_time[0], 3), round(current_time[-1], 3)])

        with open(os.path.join(destination, f"{number + 1}.txt"), 'w') as f:
            for i in range(current_time.shape[0]):
                f.write(f"{current_time[i]}\t{current_voltage[i]}\n")

    return time_intervals

# ----------------------------------------------------------------------------------------------

if __name__ == "__main__":
    C_sharp_data = sys.argv[1]

    lines = C_sharp_data.split('\n')
    destination = lines[0]
    file_path = lines[1]

    # time, voltage = open_txt(file_path)
    time, voltage = preprocess(file_path)

    # Резка ПД
    action_potentials = find_action_potentials(time, voltage, alpha_threshold=2.5, start_offset=25)

    # Сохранение в табличку
    save_aps_to_xlsx(destination, action_potentials, time, voltage)
    print("true")

# ----------------------------------------------------------------------------------------------
# Входные данные + резка пд
"""
file = "source/1.txt"
time, voltage = preprocess(file)
action_potentials = find_action_potentials(time, voltage, alpha_threshold=2.5, start_offset=25)

# Графики
plt.figure()
for ap in action_potentials:
    pre_start_index = ap['pre_start']
    end_index = ap['end']
    ap_time = time[pre_start_index:end_index + 1]
    ap_voltage = voltage[pre_start_index:end_index + 1]

    plt.plot(ap_time, ap_voltage)

plt.xlabel("Time")
plt.ylabel("Voltage")
plt.title("Merged Action Potentials")
plt.show()

for number, ap in enumerate(action_potentials):
    plot_ap(ap, time, voltage, number + 1)

plot_phase_4_speeds(action_potentials, time, voltage)
plot_phase_0_speeds(action_potentials, time, voltage)

# !!!очень долго выполняется
plot_radiuses(action_potentials, time, voltage)

# Табличка
destination = f"tables/{file.replace('.txt', '').replace('source/', '')}.xlsx"
save_aps_to_xlsx(destination, action_potentials, time, voltage)
"""
